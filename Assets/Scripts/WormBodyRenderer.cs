using UnityEngine;
using NaughtyAttributes;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Splines;

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

    [Header("Meshes")] 
    [SerializeField] private Transform meshParent;
    [SerializeField] private GameObject headMesh;
    [SerializeField] private GameObject bodyMesh;
    [SerializeField] private GameObject tailMesh;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private Color segmentColor;

    [SerializeField] private List<BodySegment> _segments = new();
    private Vector3 _previousFramePos;

    void OnEnable()
    {
        RecomputeBody();
        ReassignSegmentRadiuses();
    }

    void RecomputeBody()
    {
        if (!simulate) return;
        
        _segments.Clear();
        
        // Remove overhead or add segment radiuses 
        while (segmentRadiuses.Count != segmentCount)
        {
            if (segmentRadiuses.Count < segmentCount)
                segmentRadiuses.Add(baseRadius);
            else segmentRadiuses.Remove(segmentRadiuses[^1]);
        }
        
        // Modify segment attributes
        for (int i = 0; i < segmentCount; i++)
        {
            BodySegment newSegment = new BodySegment();
            _segments.Add(newSegment);
            
            if (i > 0)
                newSegment.parentSegment = _segments[i - 1];
            newSegment.pos = new Vector3(-i * segmentDistance, 0) + transform.localPosition;
            newSegment.radius = segmentRadiuses[i];
        }

        ReconstructMeshes();


        //Debug.Log("recomputed body");
    }

    void ReconstructMeshes()
    {
        // Destroy previous meshes (even in editor)
        var tempList = transform.Cast<Transform>().ToList();
        foreach(var child in tempList)
        {
            DestroyImmediate(child.gameObject);
        }
        
        // Spawn new meshes
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject meshToInstantiate = i == 0 ? headMesh : i == segmentCount - 1 ? tailMesh: bodyMesh;
            GameObject newMesh = Instantiate(meshToInstantiate, transform);
            newMesh.name = i == 0 ? $"WormHead_{i}" : i == segmentCount - 1 ? $"WormTail_{i}" : $"WormBody_{i}";
                
            newMesh.transform.position = _segments[i].pos;
            _segments[i].mesh = newMesh;
        }
    }

    void ReassignSegmentRadiuses()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            _segments[i].radius = segmentRadiuses[i];
            _segments[i].mesh.transform.localScale = new Vector3(segmentRadiuses[i], segmentRadiuses[i], .5f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!simulate) return;
        _previousFramePos = _segments[0].pos;
        
        if (_segments[0] == null)
            Debug.LogError($"worm body named {gameObject.name} has no segments");
        
        MoveHead();
        MoveBody();
    }

    void MoveHead()
    {
        _segments[0].pos = _segments[0].mesh.transform.position;
        
        SplineAnimate splineAnim = GetComponent<SplineAnimate>();
        float time = splineAnim.NormalizedTime;
        Vector3 tangent = splineAnim.Container.Splines[0].EvaluateTangent(time);
        
        float headAngle = Vector3.SignedAngle(Vector3.right, tangent, Vector3.up);
        _segments[0].mesh.transform.localEulerAngles = new Vector3(0, headAngle + 90, 0);
    }

    void MoveBody()
    {
        // Circle constraint each segment
        for (int i = 1; i < segmentCount; i++)
        {
            Vector3 oldPos = _segments[i].pos;
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
            _segments[i].mesh.transform.position = newPos;
            
            // Rotate segment mesh
            Vector3 deltaPos = newPos - oldPos;
            float meshAngle = Vector3.SignedAngle(Vector3.right, deltaPos, Vector3.up);
            _segments[i].mesh.transform.localEulerAngles = new Vector3(0,  meshAngle + 90, 0);
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
    public float radius;
    public GameObject mesh;
    
    public BodySegment parentSegment;
    
}