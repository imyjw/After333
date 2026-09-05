using UnityEngine;

namespace Project333.Art
{
    public class PlayerAnimationController : MonoBehaviour
    {
        private Animator animator;

        public float moveSpeed = 50f;

        void Start()
        {
            animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                animator.SetTrigger("Attack");
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                animator.SetTrigger("Die");
                Destroy(gameObject, 1f);
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                animator.SetTrigger("Run");
                transform.Translate(Vector3.right * moveSpeed * Time.deltaTime);
            }
            else if (Input.GetKey(KeyCode.LeftArrow))
            {
                animator.SetTrigger("Run");
                transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
            }
            else if (Input.GetKey(KeyCode.UpArrow))
            {
                animator.SetTrigger("Run");
                transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                animator.SetTrigger("Run");
                transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
            }
            else
            {
                animator.SetTrigger("Idle");
            }
        }
    }
}
