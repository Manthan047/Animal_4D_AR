package com.animal4d.recorder;

import android.content.Context;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaCodecList;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.media.MediaScannerConnection;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;

import java.io.File;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * High-performance, in-app video encoder for Android using hardware MediaCodec & MediaMuxer.
 * Records camera frames directly to MP4 without OS screen casting permissions or dialogs.
 */
public class CameraVideoRecorder {

    private static final String TAG = "CameraVideoRecorder";
    private static final String MIME_TYPE = "video/avc"; // H.264

    private static CameraVideoRecorder instance;

    private MediaCodec mediaCodec;
    private MediaMuxer mediaMuxer;
    private int videoTrackIndex = -1;
    private boolean isMuxerStarted = false;

    private int videoWidth;
    private int videoHeight;
    private int frameRate;
    private int bitRate;
    private int colorFormat;
    private String outputPath;
    private Context context;

    private final AtomicBoolean isRecording = new AtomicBoolean(false);
    private final LinkedBlockingQueue<FrameData> frameQueue = new LinkedBlockingQueue<>(60);
    private HandlerThread encodingThread;
    private Handler encodingHandler;
    private byte[] nvBuffer; // Reusable YUV buffer
    private int frameCount = 0;
    private long lastQueuedTimestampUs = -1;

    private static class FrameData {
        byte[] rgbaData;
        long timestampUs;
        boolean isEndOfStream;

        FrameData(byte[] rgbaData, long timestampUs, boolean isEndOfStream) {
            this.rgbaData = rgbaData;
            this.timestampUs = timestampUs;
            this.isEndOfStream = isEndOfStream;
        }
    }

    public static synchronized CameraVideoRecorder getInstance() {
        if (instance == null) {
            instance = new CameraVideoRecorder();
        }
        return instance;
    }

    /**
     * Start video recording.
     */
    public static boolean startRecording(Context ctx, String path, int width, int height, int fps, int bitrate) {
        return getInstance().start(ctx, path, width, height, fps, bitrate);
    }

    /**
     * Push raw RGBA frame to encoder queue.
     */
    public static void feedRGBAFrame(byte[] rgbaData, long timestampUs) {
        getInstance().queueFrame(rgbaData, timestampUs, false);
    }

    /**
     * Stop recording and finalize MP4 file.
     */
    public static boolean stopRecording() {
        return getInstance().stop();
    }

    /**
     * Check if currently recording.
     */
    public static boolean isRecording() {
        return getInstance().isRecording.get();
    }

    /**
     * Get the last output file path.
     */
    public static String getOutputPath() {
        return getInstance().outputPath;
    }

    /**
     * Get count of encoded frames.
     */
    public static int getFrameCount() {
        return getInstance().frameCount;
    }

    private synchronized boolean start(Context ctx, String path, int width, int height, int fps, int bitrate) {
        if (isRecording.get()) {
            Log.w(TAG, "Recording is already in progress.");
            return false;
        }

        this.context = ctx;
        this.outputPath = path;
        // Width and height must be even numbers for H.264
        this.videoWidth = (width % 2 == 0) ? width : width - 1;
        this.videoHeight = (height % 2 == 0) ? height : height - 1;
        this.frameRate = (fps > 0) ? fps : 30;
        this.bitRate = (bitrate > 0) ? bitrate : 4_000_000;
        this.nvBuffer = new byte[videoWidth * videoHeight * 3 / 2];
        this.frameQueue.clear();
        this.frameCount = 0;
        this.lastQueuedTimestampUs = -1;

        try {
            MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);
            if (codecInfo == null) {
                Log.e(TAG, "No suitable H.264 encoder found.");
                return false;
            }

            colorFormat = selectColorFormat(codecInfo, MIME_TYPE);

            MediaFormat format = MediaFormat.createVideoFormat(MIME_TYPE, videoWidth, videoHeight);
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT, colorFormat);
            format.setInteger(MediaFormat.KEY_BIT_RATE, bitRate);
            format.setInteger(MediaFormat.KEY_FRAME_RATE, frameRate);
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1); // 1s I-frame interval

            mediaCodec = MediaCodec.createByCodecName(codecInfo.getName());
            mediaCodec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            mediaCodec.start();

            File file = new File(outputPath);
            File parent = file.getParentFile();
            if (parent != null && !parent.exists()) {
                parent.mkdirs();
            }

            mediaMuxer = new MediaMuxer(outputPath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            isMuxerStarted = false;
            videoTrackIndex = -1;

            isRecording.set(true);

            // Start background encoding thread
            encodingThread = new HandlerThread("CameraVideoEncoderThread");
            encodingThread.start();
            encodingHandler = new Handler(encodingThread.getLooper());
            encodingHandler.post(this::encodingLoop);

            Log.i(TAG, "In-app camera video recording started. Output: " + outputPath + " Resolution: " + videoWidth + "x" + videoHeight);
            return true;

        } catch (Exception e) {
            Log.e(TAG, "Failed to start recording: " + e.getMessage(), e);
            release();
            return false;
        }
    }

    private void queueFrame(byte[] rgbaData, long timestampUs, boolean isEOS) {
        if (!isRecording.get() && !isEOS) return;

        if (!isEOS) {
            // Guarantee strictly increasing presentation timestamps to prevent MediaMuxer errors
            long finalUs = (timestampUs > lastQueuedTimestampUs)
                ? timestampUs
                : lastQueuedTimestampUs + (1_000_000L / Math.max(1, frameRate));
            lastQueuedTimestampUs = finalUs;

            // Drop oldest frame if queue is backing up to keep video in real-time sync
            if (frameQueue.size() >= 45) {
                frameQueue.poll();
            }

            frameQueue.offer(new FrameData(rgbaData, finalUs, false));
        } else {
            long eosUs = (lastQueuedTimestampUs > 0) ? lastQueuedTimestampUs + (1_000_000L / Math.max(1, frameRate)) : 0;
            frameQueue.offer(new FrameData(null, eosUs, true));
        }
    }

    private void encodingLoop() {
        MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();

        while (isRecording.get() || !frameQueue.isEmpty()) {
            FrameData frame = frameQueue.poll();
            if (frame != null) {
                if (frame.isEndOfStream) {
                    drainEncoder(bufferInfo, true);
                    break;
                }

                // Convert RGBA to NV12/NV21 (with vertical flip for OpenGL textures)
                if (frame.rgbaData != null && frame.rgbaData.length >= videoWidth * videoHeight * 4) {
                    rgbaToYuv(frame.rgbaData, videoWidth, videoHeight, nvBuffer, colorFormat);
                    encodeBuffer(nvBuffer, frame.timestampUs, false);
                    frameCount++;
                }

                drainEncoder(bufferInfo, false);
            } else {
                try {
                    Thread.sleep(5);
                } catch (InterruptedException ignored) {}
            }
        }

        // Finalize remaining frames
        drainEncoder(bufferInfo, true);
        release();
    }

    private void encodeBuffer(byte[] yuvData, long presentationTimeUs, boolean isEOS) {
        if (mediaCodec == null) return;

        try {
            int inputBufferIndex = mediaCodec.dequeueInputBuffer(15_000);
            int retries = 0;
            while (inputBufferIndex < 0 && retries++ < 4) {
                drainEncoder(new MediaCodec.BufferInfo(), false);
                inputBufferIndex = mediaCodec.dequeueInputBuffer(15_000);
            }

            if (inputBufferIndex >= 0) {
                ByteBuffer inputBuffer = mediaCodec.getInputBuffer(inputBufferIndex);
                if (inputBuffer != null) {
                    inputBuffer.clear();
                    if (yuvData != null && yuvData.length > 0) {
                        inputBuffer.put(yuvData, 0, Math.min(yuvData.length, inputBuffer.remaining()));
                    }
                    int flags = isEOS ? MediaCodec.BUFFER_FLAG_END_OF_STREAM : 0;
                    int length = (yuvData != null) ? yuvData.length : 0;
                    mediaCodec.queueInputBuffer(inputBufferIndex, 0, length, presentationTimeUs, flags);
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "Error in encodeBuffer: " + e.getMessage(), e);
        }
    }

    private void drainEncoder(MediaCodec.BufferInfo bufferInfo, boolean endOfStream) {
        if (mediaCodec == null) return;

        if (endOfStream) {
            try {
                int inputBufferIndex = mediaCodec.dequeueInputBuffer(15_000);
                if (inputBufferIndex >= 0) {
                    mediaCodec.queueInputBuffer(inputBufferIndex, 0, 0, 0, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                }
            } catch (Exception ignored) {}
        }

        int eosRetries = 0;
        while (true) {
            int outputBufferIndex = mediaCodec.dequeueOutputBuffer(bufferInfo, 15_000);

            if (outputBufferIndex == MediaCodec.INFO_TRY_AGAIN_LATER) {
                if (!endOfStream || ++eosRetries > 20) break;
            } else if (outputBufferIndex == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                if (!isMuxerStarted) {
                    MediaFormat newFormat = mediaCodec.getOutputFormat();
                    videoTrackIndex = mediaMuxer.addTrack(newFormat);
                    mediaMuxer.start();
                    isMuxerStarted = true;
                    Log.i(TAG, "MediaMuxer started with track index: " + videoTrackIndex);
                }
            } else if (outputBufferIndex >= 0) {
                ByteBuffer encodedData = mediaCodec.getOutputBuffer(outputBufferIndex);
                if (encodedData != null) {
                    if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) {
                        bufferInfo.size = 0;
                    }

                    if (bufferInfo.size > 0) {
                        if (!isMuxerStarted) {
                            MediaFormat newFormat = mediaCodec.getOutputFormat();
                            videoTrackIndex = mediaMuxer.addTrack(newFormat);
                            mediaMuxer.start();
                            isMuxerStarted = true;
                            Log.i(TAG, "MediaMuxer started on first buffer.");
                        }

                        encodedData.position(bufferInfo.offset);
                        encodedData.limit(bufferInfo.offset + bufferInfo.size);
                        mediaMuxer.writeSampleData(videoTrackIndex, encodedData, bufferInfo);
                    }

                    mediaCodec.releaseOutputBuffer(outputBufferIndex, false);
                }

                if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                    break;
                }
            }
        }
    }

    private synchronized boolean stop() {
        if (!isRecording.get()) return false;

        isRecording.set(false);
        queueFrame(null, 0, true);

        if (encodingThread != null) {
            try {
                encodingThread.join(4000);
            } catch (InterruptedException ignored) {}
        }

        // Trigger Android Media Scanner so it appears immediately in the gallery
        if (context != null && outputPath != null) {
            try {
                MediaScannerConnection.scanFile(
                    context,
                    new String[]{outputPath},
                    new String[]{"video/mp4"},
                    (path, uri) -> Log.i(TAG, "MediaScanner indexed video: " + path + " -> " + uri)
                );
            } catch (Exception e) {
                Log.w(TAG, "MediaScanner failed: " + e.getMessage());
            }
        }

        Log.i(TAG, "In-app camera video recording stopped. Encoded " + frameCount + " frames to: " + outputPath);
        return true;
    }

    private void release() {
        try {
            if (mediaCodec != null) {
                mediaCodec.stop();
                mediaCodec.release();
                mediaCodec = null;
            }
        } catch (Exception ignored) {}

        try {
            if (mediaMuxer != null) {
                if (isMuxerStarted && frameCount > 0) {
                    mediaMuxer.stop();
                }
                mediaMuxer.release();
                mediaMuxer = null;
            }
        } catch (Exception ignored) {}

        if (encodingThread != null) {
            encodingThread.quitSafely();
            encodingThread = null;
        }

        isMuxerStarted = false;
        videoTrackIndex = -1;
    }

    /**
     * Fast RGBA to YUV (NV12, NV21, or Planar I420) conversion with vertical flip.
     */
    private static void rgbaToYuv(byte[] rgba, int width, int height, byte[] yuv, int targetColorFormat) {
        int frameSize = width * height;
        if (rgba == null || rgba.length < frameSize * 4 || yuv == null || yuv.length < frameSize * 3 / 2) {
            return;
        }

        int yIndex = 0;
        int uvIndex = frameSize;
        int uIndex = frameSize;
        int vIndex = frameSize + (frameSize / 4);

        boolean isSemiPlanar = (targetColorFormat == MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar);
        boolean isPlanar = (targetColorFormat == MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Planar);

        // Process with vertical flip (Unity/OpenGL texture row 0 is at bottom)
        for (int j = 0; j < height; j++) {
            int srcRow = (height - 1 - j);
            int rowOffset = srcRow * width;

            for (int i = 0; i < width; i++) {
                int rgbaIndex = (rowOffset + i) * 4;
                int r = rgba[rgbaIndex] & 0xFF;
                int g = rgba[rgbaIndex + 1] & 0xFF;
                int b = rgba[rgbaIndex + 2] & 0xFF;

                // ITU-R BT.601 conversion
                int y = (66 * r + 129 * g + 25 * b + 128 >> 8) + 16;
                yuv[yIndex++] = (byte) Math.max(0, Math.min(255, y));

                if (j % 2 == 0 && i % 2 == 0) {
                    int u = (-38 * r - 74 * g + 112 * b + 128 >> 8) + 128;
                    int v = (112 * r - 94 * g - 18 * b + 128 >> 8) + 128;
                    byte uByte = (byte) Math.max(0, Math.min(255, u));
                    byte vByte = (byte) Math.max(0, Math.min(255, v));

                    if (isSemiPlanar) { // NV12
                        yuv[uvIndex++] = uByte;
                        yuv[uvIndex++] = vByte;
                    } else if (isPlanar) { // I420
                        yuv[uIndex++] = uByte;
                        yuv[vIndex++] = vByte;
                    } else { // NV21
                        yuv[uvIndex++] = vByte;
                        yuv[uvIndex++] = uByte;
                    }
                }
            }
        }
    }

    private static MediaCodecInfo selectCodec(String mimeType) {
        int numCodecs = MediaCodecList.getCodecCount();
        for (int i = 0; i < numCodecs; i++) {
            MediaCodecInfo codecInfo = MediaCodecList.getCodecInfoAt(i);
            if (!codecInfo.isEncoder()) continue;

            for (String type : codecInfo.getSupportedTypes()) {
                if (type.equalsIgnoreCase(mimeType)) {
                    return codecInfo;
                }
            }
        }
        return null;
    }

    private static int selectColorFormat(MediaCodecInfo codecInfo, String mimeType) {
        MediaCodecInfo.CodecCapabilities capabilities = codecInfo.getCapabilitiesForType(mimeType);
        for (int format : capabilities.colorFormats) {
            if (format == MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar) {
                return format; // NV12
            }
        }
        for (int format : capabilities.colorFormats) {
            if (format == MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Planar) {
                return format; // I420
            }
        }
        return MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar;
    }
}
