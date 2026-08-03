using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StudentMove : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame

    [SerializeField] float speed = 3f;   // world units per second
    [SerializeField] float targetX = 0f; // dead center, assuming the camera sits at x = 0

    void Update()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX, speed * Time.deltaTime);
        transform.position = pos;
    }
}

