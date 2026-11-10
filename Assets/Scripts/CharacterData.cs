using System.Collections.Generic;
using UnityEngine;

// Everything that makes one character different: stats, attacks, art for the menus, and the prefab to spawn.
// Create one with Assets > Create > Fighting Game > Character. Both fighters read the same asset, so a mirror match can never have mismatched numbers.
[CreateAssetMenu(menuName = "Fighting Game/Character", fileName = "NewCharacter")]
public class CharacterData : ScriptableObject
{
    [Header("identity")]
    public string displayName = "New Character";
    public Sprite portrait;     // health bar and select screen
    public GameObject prefab;   // must have a Fighter (or a subclass) on its root

    [Header("stats")]
    public int maxHealth = 100;
    public float forwardSpeed = 0.8f;
    public float backwardSpeed = 0.7f;

    [Header("hurtbox (sprite units, before the fighter's scale, facing right)")]
    public Vector2 hurtboxOffset = new Vector2(0f, 0.155f);
    public Vector2 hurtboxSize = new Vector2(0.15f, 0.31f);

    [Header("attacks (index 0, 1, 2 ... = what controllers ask for)")]
    public List<AttackData> attacks = new List<AttackData>();

    [Header("mirror match")]
    [Tooltip("Colours for player 2 when both players pick this character. Leave empty to use the same colours.")]
    public ColorPalette altPalette;
}
