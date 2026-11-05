using UnityEngine;
public class NemoBridge : MonoBehaviour
{
    public BoxCollider2D[] PresentBodies,GapBodies; public GameObject[] Visuals; public bool Present=true; public Vector2 West,East;
    public void SetPresent(bool value){Present=value;foreach(var c in PresentBodies)c.enabled=value;foreach(var c in GapBodies)c.enabled=!value;foreach(var g in Visuals)g.SetActive(value);Physics2D.SyncTransforms();}
}
