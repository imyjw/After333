using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    // 컴포넌트 참조를 위한 변수
    private Animator animator;

    public float moveSpeed = 50f;

    void Start()
    {
        // 게임 시작 시 이 오브젝트에 붙어있는 Animator 컴포넌트를 가져옵니다.
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 매 프레임마다 키 입력을 확인합니다.

        // 'S' 키를 누른 순간
        if (Input.GetKeyDown(KeyCode.S))
        {
            // 애니메이터에 'Attack' 트리거 신호를 보냅니다.
            animator.SetTrigger("Attack");
        }

        // 'D' 키를 누른 순간
        if (Input.GetKeyDown(KeyCode.D))
        {
            // 애니메이터에 'Die' 트리거 신호를 보냅니다.
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
        else{
            animator.SetTrigger("Idle");
        }
    }
}