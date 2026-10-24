using UnityEngine;


public class Myrmillo_Animator : MonoBehaviour
{
    [SerializeField] private Myrmillo_Behavior movement;
    private Animator animator;
    private static readonly int MoveDirHash = Animator.StringToHash("MoveDir");

    private void Awake() => animator = GetComponent<Animator>();

    private void Update()
    {
        animator.SetFloat(MoveDirHash, movement.MoveDirRelative);
    }
}