using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;

    public int Current { get; private set; }
    public bool IsKO => Current <= 0;

    private void Awake() => Current = maxHealth;

    public void TakeDamage(int amount)
    {
        if (IsKO) return;
        Current = Mathf.Max(0, Current - amount);
        Debug.Log(name + " health: " + Current);
    }

    public void ResetHealth() => Current = maxHealth;
}