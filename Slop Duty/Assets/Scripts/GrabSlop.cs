using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrabSlop : MonoBehaviour
{
    // Start is called before the first frame update
    bool holdingSlop ;
    string slopColor ;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Slop"))
        {
            print("collect") ;
        }
    }
}
