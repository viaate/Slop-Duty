using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PukePuddle : MonoBehaviour
{
    // Start is called before the first frame update
    SpriteRenderer puddleVisual;
    void Start()
    {
        puddleVisual = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            ChangeOpacity(-10) ;
        }
    }

    public void ChangeOpacity(int alpha)
    {
        if (((puddleVisual.color.a - alpha) > 0) && ((puddleVisual.color.a + alpha) <= 255))
        {
            puddleVisual.color = new Color(86, 154, 2, (puddleVisual.color.a + alpha)) ;
        }
        else if ((puddleVisual.color.a - alpha) <= 0)
        {
            Destroy(gameObject) ;
        }
        else
        {
            puddleVisual.color = new Color(86, 154, 2, 255) ;
        }
    }
}
