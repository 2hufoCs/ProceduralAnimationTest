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

    [Header("Torque")] 
    [SerializeField] private float torqueAmount;
    [SerializeField, Range(0.1f, 1)] private float torqueTransferCutoff;
    [SerializeField] private int maxTorqueSegments;

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

    private IKLegs _legs;
    private bool _hasLegs;

    void OnEnable()
    {
        if (TryGetComponent(out IKLegs legs))
        {
            _legs = legs;
            _hasLegs = true;
        }
        
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

        ReconstructMeshes();
        ReassignSegmentRadiuses();
        
        //Debug.Log("recomputed body");
    }

    void ReconstructMeshes()
    {
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
        
        
        // Spawn new mesh
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
                    
                    child.localPosition = Vector3.zero;
                    child.localRotation = Quaternion.identity;
                    child.localScale = Vector3.one;
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

            // Apply torque if segment has an associated leg taking a step
            if (_hasLegs)
            {
                for (int j = 0; j < _legs.legCount; j++)
                {
                    if (_legs.legBases[j].parent.parent == _segments[i].mesh.transform &&
                        _legs.segments[j][^1].takingStep)
                    {
                        ApplyTorque(i, newPos, j);
                        break;
                    }
                }
            }
            
            RotateMesh(_segments[i].pos, i);
        }
    }

    void ApplyTorque(int segmentIndex, Vector3 initialPos, int legIndex, float forceCoef = 1, int depth = 0)
    {
        if (depth >= maxTorqueSegments) return;
        
        if (legIndex == 0)
            Debug.Log($"now rotating segment n°{segmentIndex}, with strength {forceCoef}");
        
        // Get torque 
        bool rotateClockwise = legIndex % 2 == 0;
        float angleToRotate = torqueAmount * Time.deltaTime * forceCoef * (rotateClockwise ? 1 : -1);
        if (_legs.stepDuration > 0)
            angleToRotate /= _legs.stepDuration;
        
        Vector3 parentToCurrent = initialPos - _segments[segmentIndex - 1].pos;
        _segments[segmentIndex].pos = Quaternion.AngleAxis(angleToRotate, Vector3.up) * parentToCurrent + _segments[segmentIndex - 1].pos;
        
        float newForceCoef = forceCoef - torqueTransferCutoff;
        segmentIndex++;
        
        // Base case: top recursion
        if (newForceCoef <= 0 || segmentIndex >= segmentCount)
            return;
        
        // Continue rotating rest of body with less torque
        ApplyTorque(segmentIndex, _segments[segmentIndex].pos, legIndex, newForceCoef, depth + 1);
    }

    void RotateMesh(Vector3 newPos, int i)
    {
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