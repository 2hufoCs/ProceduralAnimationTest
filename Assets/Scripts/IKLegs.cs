using UnityEngine;
using System.Collections.Generic;
using NaughtyAttributes;
using System.Linq;
using System.Collections;
using UnityEngine.Splines;
using System;

[ExecuteAlways]
public class IKLegs : MonoBehaviour
{
    [SerializeField] private bool simulate = true;

    [Header("Legs")] 
    [SerializeField, OnValueChanged(nameof(RecomputeLegs))] private int legCount;
    [SerializeField, Range(1, 100), OnValueChanged(nameof(RecomputeLegs))] private int segmentCountPerLeg;

    [SerializeField] private List<bool> legDirections = new();
    [SerializeField] private List<Transform> stepEndPositions = new();
    [SerializeField] private Transform stepEndPosParent;
    [SerializeField] private int maxIKFabrikDepth;
    
    [Header("Segments")]
    [SerializeField, OnValueChanged(nameof(RecomputeLegs))] private float segmentDistance;
    [SerializeField] private float maxSegmentAngle;
    [SerializeField, Range(0, 1)] private float lookAtExtremityFactor;
    
    [SerializeField, OnValueChanged(nameof(ReassignSegmentRadiuses))] private List<float> segmentRadiuses = new();
    [SerializeField] private List<Transform> legBases = new();
    [SerializeField] private List<Transform> legPaws = new();
    private float baseRadius = .2f;

    // [Header("Meshes")] 
    // [SerializeField] private Transform meshParent;
    // [SerializeField] private GameObject headMesh;
    // [SerializeField] private GameObject bodyMesh;
    // [SerializeField] private GameObject tailMesh;

    [Header("Debug")] 
    //[SerializeField] private bool baseAnchored = true;
    [SerializeField] private bool debugMode;
    [SerializeField] private Color segmentColor;
    [SerializeField] private Transform cubeTransform;

    [SerializeField] private List<List<LegSegment>> _segments = new();
    private Vector3 _previousFramePos;
    
    
    #region Precomputed

    void OnEnable()
    {
        RecomputeLegs();
    }

    void RecomputeLegs()
    {
        if (!simulate) return;
        
        _segments.Clear();
        ResizeLists();
        
        // Modify segment attributes
        for (int j = 0; j < legCount; j++)
        {
            _segments.Add(new List<LegSegment>());
            for (int i = 0; i < segmentCountPerLeg; i++)
            {
                LegSegment newSegment = new LegSegment();
                _segments[j].Add(newSegment);
                
                newSegment.pos = new Vector3(-i * segmentDistance, 0) + legBases[j].position;
                newSegment.radius = segmentRadiuses[i];
                newSegment.mirrored = j % 2 == 0;
                newSegment.forward = legDirections[j];
            }
        }

        // ReconstructMeshes();
        ReassignSegmentRadiuses();
        
        //Debug.Log("recomputed body");
    }

    void ResizeLists()
    {
        // Remove overhead or add leg directions
        while (legDirections.Count != legCount || stepEndPositions.Count != legCount)
        {
            if (legDirections.Count < legCount)
                legDirections.Add(true);
            else legDirections.Remove(legDirections[^1]);
            
            if (stepEndPositions.Count < legCount)
                stepEndPositions.Add(transform);
            else stepEndPositions.Remove(stepEndPositions[^1]);
        }
        
        // Remove overhead or add segment radiuses 
        while (segmentRadiuses.Count != segmentCountPerLeg)
        {
            if (segmentRadiuses.Count < segmentCountPerLeg)
                segmentRadiuses.Add(baseRadius);
            else segmentRadiuses.Remove(segmentRadiuses[^1]);
        }
        
        while (legBases.Count != legCount || legPaws.Count != legCount)
        {
            // Leg bases
            if (legBases.Count < legCount)
                legBases.Add(transform);
            else legBases.Remove(legBases[^1]);
            
            // Leg paws
            if (legPaws.Count < legCount)
                legPaws.Add(transform);
            else legPaws.Remove(legPaws[^1]);
        }
    }

    void ReassignSegmentRadiuses()
    {
        for (int j = 0; j < legCount; j++)
        {
            for (int i = 0; i < segmentCountPerLeg; i++)
            {
                _segments[j][i].radius = segmentRadiuses[i];
                //_segments[i].mesh.transform.localScale = new Vector3(segmentRadiuses[i], segmentRadiuses[i], .5f);
            }
        }
    }
    
    // void ReconstructMeshes()
    // {
    //     // Destroy previous meshes (even in editor)
    //     var tempList = meshParent.Cast<Transform>().ToList();
    //     foreach(var child in tempList)
    //     {
    //         DestroyImmediate(child.gameObject);
    //     }
    //     
    //     // Spawn new meshes
    //     for (int i = 0; i < segmentCount; i++)
    //     {
    //         GameObject meshToInstantiate = i == 0 ? headMesh : i == segmentCount - 1 ? tailMesh: bodyMesh;
    //         GameObject newMesh = Instantiate(meshToInstantiate, meshParent);
    //         newMesh.name = i == 0 ? $"WormHead_{i}" : i == segmentCount - 1 ? $"WormTail_{i}" : $"WormBody_{i}";
    //             
    //         newMesh.transform.position = _segments[i].pos;
    //         _segments[i].mesh = newMesh;
    //     }
    // }
    
    #endregion Precomputed
    
    #region Looping
    
    // Update is called once per frame
    void Update()
    {
        if (!simulate) return;
        if (_segments[0] == null)
            Debug.LogError($"leg named {gameObject.name} has no segments");
        
        RotateStepEndPos();
        
        foreach (List<LegSegment> legSegments in _segments)
            MoveFabrikIK(legSegments);
        //MoveFabrikIK(_segments[1]);
    }
    
    void MoveFabrikIK(List<LegSegment> legSegments)
    {
        // List<LegSegment> legSegments = _segments[0];
        bool isBaseAnchored = false;
        
        Debug.Log("starting IK Fabrik algorithm");
        for (int depth = 0; depth < maxIKFabrikDepth; depth++)
        {
            MoveLegFK(legSegments, isBaseAnchored);
            isBaseAnchored = !isBaseAnchored;
        }
        Debug.Log("finished IK Fabrik algorithm");
    }

    /// <summary>
    /// Makes the leg follow one extremity
    /// </summary>
    void MoveLegFK(List<LegSegment> legSegments, bool isBaseAnchored)
    {
        int legIndex = _segments.IndexOf(legSegments);
        if (isBaseAnchored)
            legSegments[0].pos = legBases[legIndex].position;
        else
            legSegments[^1].pos = legPaws[legIndex].position;
        
        // Circle constraint each segment
        for (int i = isBaseAnchored ? 1 : segmentCountPerLeg - 2; isBaseAnchored ? i < segmentCountPerLeg: i >= 0; i += isBaseAnchored ? 1 : -1)
        {
            LegSegment parentSeg = legSegments[isBaseAnchored ? i - 1 : i + 1];
            
            Vector3 posDiff = legSegments[i].pos - parentSeg.pos;
            Vector3 newPos = parentSeg.pos + posDiff.normalized * segmentDistance;
            
            bool angleCheckCondition = isBaseAnchored ? i > 1 : i < segmentCountPerLeg - 2;
            if (angleCheckCondition)
            {
                LegSegment parentSeg2 = legSegments[isBaseAnchored ? i - 2 : i + 2];
                
                // Check neighboring segments and calculate angle
                Vector3 v1 = parentSeg2.pos - parentSeg.pos;
                Vector3 v2 = newPos - parentSeg.pos;
                float angle = Vector3.SignedAngle(v1, v2, Vector3.up);

                if (isBaseAnchored)
                {
                    DrawArrow.ForDebug(parentSeg.pos, v1, Color.red);
                    DrawArrow.ForDebug(parentSeg.pos, v2, Color.blue);
                }

            
                // Clamp angle
                if (Mathf.Abs(angle) < maxSegmentAngle)
                    newPos = Quaternion.AngleAxis(maxSegmentAngle * Mathf.Sign(angle), Vector3.up) * v1 + parentSeg.pos;
                
                // Rotate legs depending on forward/backward
                bool positiveAngle = Mathf.Sign(angle) < .01;
                bool rotateLegSegment;
                
                if ((legSegments[i].forward && legSegments[i].mirrored) || (!legSegments[i].forward && !legSegments[i].mirrored))
                    rotateLegSegment = isBaseAnchored && !positiveAngle || !isBaseAnchored && positiveAngle;
                else
                    rotateLegSegment = isBaseAnchored && positiveAngle || !isBaseAnchored && !positiveAngle;
                
                if (rotateLegSegment)
                {
                    // Step 1: calculate angle diff
                    float deltaAngle = Vector3.SignedAngle((parentSeg.pos - newPos).normalized, (parentSeg2.pos - newPos).normalized, Vector3.up);
                    parentSeg.pos = newPos + Quaternion.AngleAxis(deltaAngle * 2, Vector3.up) * (parentSeg.pos - newPos);
                }
            }
            
            legSegments[i].pos = newPos;
            
            
            //_segments[i].mesh.transform.position = newPos;
            
            // Rotate segment mesh
            // float nextSegmentAngle = _segments[i - 1].mesh.transform.eulerAngles.y;
            // Vector3 nextExtremity = Quaternion.AngleAxis(nextSegmentAngle - 90, Vector3.up) * Vector3.left * .5f + _segments[i - 1].pos;
            // Vector3 lookAtPos = Lerp(_segments[i - 1].pos, nextExtremity, lookAtExtremityFactor);
            //
            // Vector3 deltaPos = lookAtPos - newPos;
            // float meshAngle = Vector3.SignedAngle(Vector3.right, deltaPos, Vector3.up);
            // _segments[i].mesh.transform.localEulerAngles = new Vector3(0,  meshAngle + 90, 0);
            
            // Scale to fill gaps
            // float angleDiff = Vector3.Angle(_segments[i].mesh.transform.forward, _segments[i - 1].mesh.transform.forward);
            // float scaleFactor = Mathf.Lerp(1, 1.35f, angleDiff / 45f);
            // _segments[i].mesh.transform.localScale = new Vector3(_segments[i].mesh.transform.localScale.x, _segments[i].mesh.transform.localScale.y, .5f * scaleFactor);
        }

        // if (isBaseAnchored)
        //     legPaws[legIndex].position = legSegments[^1].pos;
        // else
        //     legBases[legIndex].position = legSegments[0].pos;
        
    }


    void RotateStepEndPos()
    {
        SplineAnimate splineAnim = GetComponent<SplineAnimate>();
        float time = splineAnim.NormalizedTime;
        Vector3 tangent = splineAnim.Container.Splines[0].EvaluateTangent(time);
        
        float headAngle = Vector3.SignedAngle(Vector3.right, tangent, Vector3.up);
        stepEndPosParent.transform.localEulerAngles = new Vector3(0, headAngle, 0);
    }
    
    
    #endregion Looping

    Vector3 Lerp(Vector3 v1, Vector3 v2, float t)
    {
        return new Vector3(v1.x + (v2.x - v1.x) * t, v1.y + (v2.y - v1.y) * t, v1.z + (v2.z - v1.z) * t);
    }

    void OnDrawGizmos()
    {
        if (!debugMode || !simulate) return;

        foreach (List<LegSegment> legSegments in _segments)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(stepEndPositions[_segments.IndexOf(legSegments)].position, .3f);
            foreach (LegSegment segment in legSegments)
            {
                Gizmos.color = segmentColor;
                Gizmos.DrawSphere(segment.pos, segment.radius);
            }
        }
    }
}

[Serializable]
public class LegSegment
{
    public Vector3 pos;
    public float radius;
    
    public bool forward;
    public bool mirrored;
}