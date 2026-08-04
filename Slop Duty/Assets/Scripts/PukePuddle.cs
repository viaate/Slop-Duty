using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PukePuddle : MonoBehaviour
{
    // Start is called before the first frame update
    SpriteRenderer puddleVisual ;
    void Start()
    {
        puddleVisual = GetComponent<SpriteRenderer>() ;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ChangeOpacity(int alpha)
    {
        puddleVisual.color = new Color(86, 154, 2, alpha) ;
    }
}
