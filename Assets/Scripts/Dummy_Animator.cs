using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Dummy_Animator : MonoBehaviour
{
    [SerializeField] private DummyBehaviour dummy;
    private Animator animator;
    private static readonly int IsHitstunHash = Animator.StringToHash("IsHitstun");
    private static readonly int IsBlockstunHash = Animator.StringToHash("IsBlockstun");
    private static readonly int HitHash = Animator.StringToHash("hit");
    private static readonly int BlockHash = Animator.StringToHash("block");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (dummy == null) dummy = GetComponentInParent<DummyBehaviour>();
        if (dummy == null) Debug.LogWarning(name + ": Dummy_Animator has no DummyBehaviour assigned");
    }

    private void Start()
    {
        if (!animator.HasState(0, HitHash)) Debug.LogWarning(name + ": animator has no 'hit' state");
        if (!animator.HasState(0, BlockHash)) Debug.LogWarning(name + ": animator has no 'block' state");
    }

    private void OnEnable()
    {
        if (dummy != null) dummy.HitReceived += OnHitReceived;
    }

    private void OnDisable()
    {
        if (dummy != null) dummy.HitReceived -= OnHitReceived;
    }

    private void Update()
    {
        if (dummy == null) return;
        animator.SetBool(IsHitstunHash, dummy.IsHitstun);
        animator.SetBool(IsBlockstunHash, dummy.IsBlockstun);
    }

    // Restart the stun clip on every hit, so a second hit during stun plays it again
    private void OnHitReceived(BaseFighter.HitType type, AttackData atk)
    {
        if (type == BaseFighter.HitType.None) return;
        animator.Play(type == BaseFighter.HitType.Blocked ? BlockHash : HitHash, 0, 0f);
    }
}
