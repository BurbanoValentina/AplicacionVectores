using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eldvmo.Ripples
{
    public class BoatControl : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float _speed = 3f;         
        [SerializeField] private float _rotateSpeed = 90f;   

        [Header("Inclinación visual")]
        [SerializeField] private float _maxRoll = 15f;      
        [SerializeField] private float _maxPitch = 8f;      
        [SerializeField] private float _tiltSmooth = 6f;    

        float _targetRoll;   
        float _targetPitch; 

        void Update()
        {

            float move = 0f;  
            float turn = 0f;  

            if (Input.GetKey(KeyCode.W)) move += 1f;
            if (Input.GetKey(KeyCode.S)) move -= 1f;


            if (Input.GetKey(KeyCode.A)) { turn -= 1f; move += 1f; }
            if (Input.GetKey(KeyCode.D)) { turn += 1f; move += 1f; }

            move = Mathf.Clamp(move, -1f, 1f);
            turn = Mathf.Clamp(turn, -1f, 1f);

            if (Mathf.Abs(move) > 0.001f)
                transform.Translate(Vector3.forward * (move * _speed * Time.deltaTime), Space.Self);

            if (Mathf.Abs(turn) > 0.001f)
                transform.Rotate(Vector3.up * (turn * _rotateSpeed * Time.deltaTime), Space.Self);

            _targetRoll  = -turn * _maxRoll;   
            _targetPitch =  move * _maxPitch;  

            Vector3 e = transform.localEulerAngles; 
            float yaw   = e.y;
            float roll  = Mathf.LerpAngle(Normalize(e.z), _targetRoll,  Time.deltaTime * _tiltSmooth);
            float pitch = Mathf.LerpAngle(Normalize(e.x), _targetPitch, Time.deltaTime * _tiltSmooth);

            transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
        }

        float Normalize(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }
    }
}
