using UnityEngine;
using NaughtyAttributes;
using System.Collections.Generic;
using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.Splines;

[ExecuteAlways]
public class WormBodyRenderer : MonoBehaviour
{
    [SerializeField] private bool simulate = true;
    
    [Header("Body")]
    [SerializeField, Range(1, 20), OnValueChanged(nameof(RecomputeBody))] private int segmentCount;
    [SerializeField, OnValueChanged(nameof(ReassignSegmentRadiuses))] private List<float> segmentRadiuses = new();
    [SerializeField, OnValueChanged(nameof(RecomputeBody))] private float segmentDistance;
    private float baseRadius = .4f;
    
    [SerializeField] private float maxSegmentAngle;
    [SerializeField, Range(0, 1)] private float lookAtExtremityFactor;

    [Header("Meshes")] 
    [SerializeField] private Transform meshParent;
    [SerializeField] private GameObject headMesh;
    [SerializeField] private GameObject bodyMesh;
    [SerializeField] private GameObject tailMesh;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private Color segmentColor;
    [SerializeField] private Transform cubeTransform;

    [SerializeField] private List<BodySegment> _segments = new();
    private Vector3 _previousFramePos;

    void OnEnable()
    {
        RecomputeBody();
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

        Debug.Log("supposed to reconstruct meshes");
        ReconstructMeshes();
        ReassignSegmentRadiuses();
        
        //Debug.Log("recomputed body");
    }

    void ReconstructMeshes()
    {
        // Destroy previous meshes (even in editor)

        
        // Debug.Log($"number of meshes: {childList.Count}, segment count:  {segmentCount}");
        // while (childList.Count != segmentCount)
        // {
        //     if (_segments.Count > segmentCount)
        //     {
        //         DestroyImmediate(childList[^1]);
        //         childList = meshParent.Cast<Transform>().ToList();
        //         continue;
        //     }
        //     
        //     
        // }
        Dictionary<int, List<Transform>> childrenToReassign = new();
        var childList = meshParent.Cast<Transform>().ToList();
        
        for (int i = 0; i < childList.Count; i++)
        {
            Transform child = childList[i];
            var subChildren = child.Cast<Transform>().ToList();
            List<Transform> subChildList = new();
            
            foreach(var subChild in subChildren)
            {
                if (subChild.name.Contains("LegsPivot"))
                {
                    Debug.Log("prevented destroy of legs");
                    subChild.parent = transform;
                    subChildList.Add(subChild);
                    
                    subChild.localPosition = Vector3.zero;
                    subChild.localRotation = Quaternion.identity;
                    subChild.localScale = Vector3.one;
                }
            }
            childrenToReassign.Add(i, subChildList);

            DestroyImmediate(child.gameObject);
        }
        
        
        // Spawn new meshes
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject meshToInstantiate = i == 0 ? headMesh : i == segmentCount - 1 ? tailMesh: bodyMesh;
            GameObject newMesh = Instantiate(meshToInstantiate, meshParent);
            newMesh.name = i == 0 ? $"WormHead_{i}" : i == segmentCount - 1 ? $"WormTail_{i}" : $"WormBody_{i}";
                
            newMesh.transform.position = _segments[i].pos;
            _segments[i].mesh = newMesh;

            if (childrenToReassign.TryGetValue(i, out List<Transform> children))
            {
                foreach (Transform child in children)
                {
                    child.parent = newMesh.transform;
                }
            }
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
            Vector3 posDiff = _segments[i].pos - _segments[i - 1].pos;
            Vector3 newPos = _segments[i - 1].pos + posDiff.normalized * segmentDistance;
            
            // If angle too small, clamp it 
            if (i >= 2)
            {
                Vector3 v1 = _segments[i - 2].pos - _segments[i - 1].pos;
                Vector3 v2 = newPos - _segments[i - 1].pos;
                float angle = Vector3.SignedAngle(v1, v2, Vector3.up);
            
                if (Mathf.Abs(angle) < maxSegmentAngle)
                    newPos = Quaternion.AngleAxis(maxSegmentAngle * Mathf.Sign(angle), Vector3.up) * v1 + _segments[i - 1].pos;
            }
            
            _segments[i].pos = newPos;
            _segments[i].mesh.transform.position = newPos;
            
            // Rotate segment mesh
            float nextSegmentAngle = _segments[i - 1].mesh.transform.eulerAngles.y;
            Vector3 nextExtremity = Quaternion.AngleAxis(nextSegmentAngle - 90, Vector3.up) * Vector3.left * .5f + _segments[i - 1].pos;
            Vector3 lookAtPos = Lerp(_segments[i - 1].pos, nextExtremity, lookAtExtremityFactor);
            // if (i == 1)
            //     cubeTransform.position = nextExtremity;
            
            Vector3 deltaPos = lookAtPos - newPos;
            float meshAngle = Vector3.SignedAngle(Vector3.right, deltaPos, Vector3.up);
            _segments[i].mesh.transform.localEulerAngles = new Vector3(0,  meshAngle + 90, 0);
            
            // Scale to fill gaps
            float angleDiff = Vector3.Angle(_segments[i].mesh.transform.forward, _segments[i - 1].mesh.transform.forward);
            float scaleFactor = Mathf.Lerp(1, 1.35f, angleDiff / 45f);
            _segments[i].mesh.transform.localScale = new Vector3(_segments[i].mesh.transform.localScale.x, _segments[i].mesh.transform.localScale.y, .5f * scaleFactor);
        }
    }

    Vector3 Lerp(Vector3 v1, Vector3 v2, float t)
    {
        return new Vector3(v1.x + (v2.x - v1.x) * t, v1.y + (v2.y - v1.y) * t, v1.z + (v2.z - v1.z) * t);
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