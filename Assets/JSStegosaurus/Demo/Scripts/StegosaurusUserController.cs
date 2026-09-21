using UnityEngine;
using System.Collections;

namespace JSukoAnimals
{
    public class StegosaurusUserController : MonoBehaviour
    {
        StegosaurusCharacter stegosaurusCharacter;

        void Start()
        {
            stegosaurusCharacter = GetComponent<StegosaurusCharacter>();
        }

        void Update()
        {
            if (Input.GetButtonDown("Fire1"))
            {
                stegosaurusCharacter.Attack();
            }
            if (Input.GetButtonDown("Jump"))
            {
                stegosaurusCharacter.Jump();
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                stegosaurusCharacter.Hit();
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                stegosaurusCharacter.Death();
            }
            if (Input.GetKeyDown(KeyCode.L))
            {
                stegosaurusCharacter.Rebirth();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                stegosaurusCharacter.Gallop();
            }
            if (Input.GetKeyUp(KeyCode.X))
            {
                stegosaurusCharacter.Walk();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                stegosaurusCharacter.EatStart();
            }
            if (Input.GetKeyUp(KeyCode.E))
            {
                stegosaurusCharacter.EatEnd();
            }

            stegosaurusCharacter.forwardSpeed = stegosaurusCharacter.maxWalkSpeed * Input.GetAxis("Vertical");
            stegosaurusCharacter.turnSpeed = Input.GetAxis("Horizontal");
        }
    }
}