using UnityEngine;
using System.Collections;

namespace JSukoAnimals
{
    public class StegosaurusCharacter : MonoBehaviour
    {
        Animator stegosaurusAnimator;
        public bool jumpStart = false;
        public float groundCheckDistance = 0.6f;
        public float groundCheckOffset = 0.01f;
        public bool isGrounded = true;
        public float jumpSpeed = 1f;
        Rigidbody stegosaurusRigid;
        public float forwardSpeed;
        public float turnSpeed;
        public float walkMode = 1f;
        public float jumpStartTime = 0f;
        public float maxWalkSpeed = 1f
    ;
        void Start()
        {
            stegosaurusAnimator = GetComponent<Animator>();
            stegosaurusRigid = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            CheckGroundStatus();
            Move();
            jumpStartTime += Time.deltaTime;
            maxWalkSpeed = Mathf.Lerp(maxWalkSpeed, walkMode, Time.deltaTime);
        }

        public void Attack()
        {
             Debug.Log("Attack() called, animator=" + (stegosaurusAnimator != null ? stegosaurusAnimator.name : "NULL"));
            stegosaurusAnimator.SetTrigger("Attack");
        }

        public void Hit()
        {
            stegosaurusAnimator.SetTrigger("Hit");
        }

        public void Death()
        {
            stegosaurusAnimator.SetBool("IsLived", false);
        }

        public void Rebirth()
        {
            stegosaurusAnimator.SetBool("IsLived", true);
        }

        public void EatStart()
        {
            stegosaurusAnimator.SetBool("IsEating", true);
        }

        public void EatEnd()
        {
            stegosaurusAnimator.SetBool("IsEating", false);
        }

        public void Gallop()
        {
            walkMode = 3f;
        }


        public void Walk()
        {
            walkMode = 1f;
        }

        public void Jump()
        {
            if (isGrounded)
            {
                stegosaurusAnimator.SetTrigger("Jump");
                jumpStart = true;
                jumpStartTime = 0f;
                isGrounded = false;
                stegosaurusAnimator.SetBool("IsGrounded", false);
            }
        }

        void CheckGroundStatus()
        {
            RaycastHit hitInfo;
            isGrounded = Physics.Raycast(transform.position + (transform.up * groundCheckOffset), Vector3.down, out hitInfo, groundCheckDistance);

            if (jumpStart)
            {
                if (jumpStartTime > .25f)
                {
                    jumpStart = false;
                    stegosaurusRigid.AddForce((transform.up + transform.forward * forwardSpeed) * jumpSpeed, ForceMode.Impulse);
                    stegosaurusAnimator.applyRootMotion = false;

                    stegosaurusAnimator.SetBool("IsGrounded", false);
                }
            }

            if (isGrounded && !jumpStart && jumpStartTime > .5f)
            {
                stegosaurusAnimator.applyRootMotion = true;

                stegosaurusAnimator.SetBool("IsGrounded", true);
            }
            else
            {
                if (!jumpStart)
                {
                    stegosaurusAnimator.applyRootMotion = false;

                    stegosaurusAnimator.SetBool("IsGrounded", false);
                }
            }
        }

        public void Move()
        {
            stegosaurusAnimator.SetFloat("Forward", forwardSpeed);
            stegosaurusAnimator.SetFloat("Turn", turnSpeed);
        }
    }
}