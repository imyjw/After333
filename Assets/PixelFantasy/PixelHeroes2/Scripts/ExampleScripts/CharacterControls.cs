using Assets.PixelFantasy.Common.Scripts;
using UnityEngine;

namespace Assets.PixelFantasy.PixelHeroes2.Scripts.ExampleScripts
{
    [RequireComponent(typeof(Creature))]
    [RequireComponent(typeof(CharacterController2D))]
    [RequireComponent(typeof(CharacterAnimation))]
    public class CharacterControls : MonoBehaviour
    {
        private Creature _character;
        private CharacterController2D _controller;
        private CharacterAnimation _animation;

        public void Start()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<CharacterController2D>();
            _animation = GetComponent<CharacterAnimation>();
        }

        public void Update()
        {
            Move();
            Attack();

            // Play other animations, just for example.
            if (Input.GetKeyDown(KeyCode.I)) _animation.Idle();
            if (Input.GetKeyDown(KeyCode.W)) _animation.Walk();
            if (Input.GetKeyDown(KeyCode.R)) _animation.Run();
            if (Input.GetKeyDown(KeyCode.D)) _animation.Die();
            if (Input.GetKeyUp(KeyCode.L)) EffectManager.Instance.Blink(_character);
        }

        private void Move()
        {
            _controller.Input = Vector2.zero;

            if (Input.GetKey(KeyCode.LeftArrow))
            {
                _controller.Input.x = -1;
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                _controller.Input.x = 1;
            }

            if (Input.GetKey(KeyCode.UpArrow))
            {
                _controller.Input.y = 1;
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                _controller.Input.y = -1;
            }
        }

        private void Attack()
        {
            if (Input.GetKeyDown(KeyCode.S)) _animation.Slash();
        }
    }
}