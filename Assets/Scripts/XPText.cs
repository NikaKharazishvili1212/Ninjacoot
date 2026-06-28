using UnityEngine;
using TMPro;
using Nikson;
using static Utils;
using static GameConstants;

public class XPText : MonoBehaviour
{
    public static Transform Cam; // Initialized by GameManager
    [SerializeField] TextMeshPro xpText;

    public void ShowXPText(Transform target, int xpAmount)
    {
        transform.position = target.position + Vector3.up * 2;
        xpText.text = "XP +" + xpAmount.ToString();
        gameObject.SetActive(true);
        this.Wait(XpTextLifespan, () => gameObject.SetActive(false));
    }

    void Update()
    {
        SetScreenSizeBillboard(transform, Cam, 0.3f);
        MoveTransformUp(transform, XpTextMoveUpSpeed);
    }
}