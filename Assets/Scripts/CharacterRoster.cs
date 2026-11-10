using System.Collections.Generic;
using UnityEngine;

// The characters you can pick, in menu order. Create with Assets > Create > Fighting Game > Roster.
[CreateAssetMenu(menuName = "Fighting Game/Roster", fileName = "Roster")]
public class CharacterRoster : ScriptableObject
{
    public List<CharacterData> characters = new List<CharacterData>();
}
