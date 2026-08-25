using UnityEngine;
using NaughtyAttributes;
using System.Collections.Generic;
using System;

[ExecuteAlways]
public class WormBodyRenderer : MonoBehaviour
{
    [SerializeField] private bool simulate = true;
    
    [Header("Body")]
    [SerializeField, Range(1, 20), OnValueChanged(nameof(RecomputeBody))] private int segmentCount;
    [SerializeField, OnValueChanged(nameof(ReassignSegmentRadiuses))] private List<float> segmentRadiuses = new();
    [SerializeField] private float baseRadius;
    [SerializeField, OnValueChanged(nameof(RecomputeBody))] private float segmentDistance;
    [SerializeField] private float maxSegmentAngle;

    [SerializeField] private float moveSpeed;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private Color segmentColor;

    [SerializeField] private List<BodySegment> _segments = new();

    void OnEnable()
    {
        RecomputeBody();
    }

    void RecomputeBody()
    {
        if (!simulate) return;
        
        _segments.Clear();
        while (segmentRadiuses.Count != segmentCount)
        {
            if (segmentRadiuses.Count < segmentCount)
                segmentRadiuses.Add(baseRadius);
            else segmentRadiuses.Remove(segmentRadiuses[^1]);
        }
        
        for (int i = 0; i < segmentCount; i++)
        {
            BodySegment newSegment = new BodySegment();
            _segments.Add(newSegment);
            
            if (i > 0)
                newSegment.parentSegment = _segments[i - 1];
            newSegment.pos = new Vector3(-i * segmentDistance, 0) + transform.localPosition;
            newSegment.radius = segmentRadiuses[i];
        }
        //Debug.Log("recomputed body");
    }

    void ReassignSegmentRadiuses()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            _segments[i].radius = segmentRadiuses[i];
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!simulate) return;
        if (_segments[0] == null)
            Debug.LogError($"worm body named {gameObject.name} has no segments");
        
        // Make head follow object position
        _segments[0].pos = transform.position;
        MoveBody();
    }

    void MoveBody()
    {
        // Circle constraint each segment
        for (int i = 1; i < segmentCount; i++)
        {
            Vector3 posDiff = _segments[i].pos - _segments[i - 1].pos;
            Vector3 newPos = _segments[i - 1].pos + posDiff.normalized * segmentDistance;
            
            // If angle too small, clamp it 
            if (i >= 2)
            {
                Vector3 v1 = _segments[i - 2].pos - _segments[i - 1].pos;
                Vector3 v2 = newPos - _segments[i - 1].pos;
                float angle = Vector3.SignedAngle(v1, v2, Vector3.up);
                
                // if (i == segmentCount - 1 && angle < maxSegmentAngle)
                //     Debug.Log("angle is " + angle);
                //Debug.Log($"right vector rotated by 135 degrees: {Quaternion.AngleAxis(maxSegmentAngle, Vector3.up) * Vector3.right}");
            
                if (Mathf.Abs(angle) < maxSegmentAngle)
                {
                    //float angleToRotate = (maxSegmentAngle - Mathf.Abs(angle)) * Mathf.Sign(angle);
                    newPos = v1;
                    newPos = Quaternion.AngleAxis(maxSegmentAngle * Mathf.Sign(angle), Vector3.up) * newPos + _segments[i - 1].pos;
                }
            }
            
            _segments[i].pos = newPos;
        }
    }

    void OnDrawGizmos()
    {
        if (!debugMode || !simulate) return;
        
        foreach (BodySegment segment in _segments)
        {
            Gizmos.color = segmentColor;
            Gizmos.DrawSphere(segment.pos, segment.radius);
        }
    }
}

[Serializable]
public class BodySegment
{
    public Vector3 pos;
    public BodySegment parentSegment;
    public float radius;
}