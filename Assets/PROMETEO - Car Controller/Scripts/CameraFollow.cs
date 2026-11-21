using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour {

	public Transform carTransform;
	[Range(1, 10)]
	public float followSpeed = 2;
	[Range(1, 10)]
	public float lookSpeed = 5;
	Vector3 initialCameraPosition;
	Vector3 initialCarPosition;
	Vector3 absoluteInitCameraPosition;

	void Start(){
		initialCameraPosition = gameObject.transform.position;
		initialCarPosition = carTransform.position;
		absoluteInitCameraPosition = initialCameraPosition - initialCarPosition;
	}

    void FixedUpdate()
    {
        if (GameState.IsPaused) return;

        //Look at car
        Vector3 _lookDirection = carTransform.position - transform.position;
        if (_lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion _rot = Quaternion.LookRotation(_lookDirection, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, _rot, lookSpeed * Time.deltaTime);
        }

        //Move to car
        Vector3 _targetPos = absoluteInitCameraPosition + carTransform.position;
        transform.position = Vector3.Lerp(transform.position, _targetPos, followSpeed * Time.deltaTime);
    }

}
