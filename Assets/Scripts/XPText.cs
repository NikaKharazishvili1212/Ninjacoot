using UnityEngine;
using TMPro;
using static Utils;
using static GameConstants;

public class XPText : MonoBehaviour
{
    public static Transform Cam;
    [SerializeField] TextMeshPro xpText;

    public void ShowXPText(Transform target, int xpAmount)
    {
        transform.position = target.position + Vector3.up * 2;
        xpText.text = "XP +" + xpAmount.ToString();
        gameObject.SetActive(true);
        this.Invoke2(XpTextLifespan, () => gameObject.SetActive(false));
    }

    void Update()
    {
        SetScreenSizeBillboard(transform, Cam, 0.3f);
        MoveTransformUp(transform, XpTextMoveUpSpeed);
    }
}