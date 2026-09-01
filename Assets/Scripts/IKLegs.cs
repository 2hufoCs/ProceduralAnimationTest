using UnityEngine;
using System.Collections.Generic;
using NaughtyAttributes;
using System.Linq;
using System.Collections;
using UnityEngine.Splines;
using System;
using DG.Tweening;

[ExecuteAlways]
public class IKLegs : MonoBehaviour
{
    [SerializeField] private bool simulate = true;
    [SerializeField] private bool ikEditMode;

    [Header("Legs")] 
    [OnValueChanged(nameof(RecomputeLegs))] public int legCount;
    [SerializeField, Range(1, 100), OnValueChanged(nameof(RecomputeLegs))] private int segmentCountPerLeg;

    [SerializeField] private List<bool> legDirections = new();
    [SerializeField] private List<Transform> stepEndPositions = new();
    [SerializeField] private List<Transform> stepBeginPositions = new();
    [SerializeField] private int maxIKFabrikDepth;
    
    [Header("Segments")]
    [SerializeField, OnValueChanged(nameof(RecomputeLegs))] private float segmentDistance;
    [SerializeField] private float maxSegmentAngle;
    [SerializeField, Range(0, 1)] private float lookAtExtremityFactor;
    
    [SerializeField, OnValueChanged(nameof(ReassignSegmentRadiuses))] private List<float> segmentRadiuses = new();
    public List<Transform> legBases = new();
    [SerializeField] private List<Transform> legPaws = new();
    private float baseRadius = .2f;

    public float stepDuration;

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

    public List<List<LegSegment>> segments = new();
    private Vector3 _previousFramePos;
    
    
    #region Precomputed

    void OnEnable()
    {
        RecomputeLegs();
    }

    void RecomputeLegs()
    {
        if (!simulate) return;
        
        segments.Clear();
        ResizeLists();
        
        // Modify segment attributes
        for (int j = 0; j < legCount; j++)
        {
            segments.Add(new List<LegSegment>());
            for (int i = 0; i < segmentCountPerLeg; i++)
            {
                LegSegment newSegment = new LegSegment();
                segments[j].Add(newSegment);
                
                newSegment.pos = new Vector3(-i * segmentDistance, 0) + legBases[j].position;
                newSegment.radius = segmentRadiuses[i];
                newSegment.mirrored = j % 2 == 0;
                newSegment.forward = legDirections[j];
            }
        }

        // ReconstructMeshes();
        ReassignSegmentRadiuses();
        
        //Debug.Log("recomputed body");
        
        // foreach (List<LegSegment> legSegments in segments)
        //     MoveLegFK(legSegments, true);
    }

    void ResizeLists()
    {
        // Remove overhead or add leg directions
        while (legDirections.Count != legCount || stepEndPositions.Count != legCount || stepBeginPositions.Count != legCount)
        {
            if (legDirections.Count < legCount)
                legDirections.Add(true);
            else legDirections.Remove(legDirections[^1]);
            
            if (stepEndPositions.Count < legCount)
                stepEndPositions.Add(transform);
            else stepEndPositions.Remove(stepEndPositions[^1]);
            
            if (stepBeginPositions.Count < legCount)
                stepBeginPositions.Add(transform);
            else  stepBeginPositions.Remove(stepBeginPositions[^1]);
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

        // stepBeginStartLocalPos.Clear();
        // for (int i = 0; i < legCount; i++)
        // {
        //     // 1. Set parent to pivot
        //     Transform t = stepBeginPositions[i];
        //     Vector3 pos = t.position;
        //     Debug.Log("position before removing: " + t.localPosition);
        //     t.parent = legBases[i].parent;
        //     
        //     // 2. Store local position
        //     t.position = pos;
        //     //stepBeginStartLocalPos.Add(t.localPosition);
        //     
        //     // TODO: remove hardcoded values once this system work
        //     Vector3 localPos = new Vector3((i % 2 == 0) ? -3 : 3, 0, -1.8f);
        //     if (i >= 2) localPos.x *= 1.5f;
        //     stepBeginStartLocalPos.Add(localPos);
        //     t.localPosition = localPos;
        //     Debug.Log("local position assigning pivot as parent: " + t.localPosition);
        //     
        //     // 3. Reset parent to snake transform
        //     t.parent = transform;
        //     //t.position = pos;
        //     t.localRotation = Quaternion.identity;
        //     t.localScale = Vector3.one;
        //     Debug.Log("position after reassigning snake parent: " + t.localPosition);
        // }
    }

    void ReassignSegmentRadiuses()
    {
        for (int j = 0; j < legCount; j++)
        {
            for (int i = 0; i < segmentCountPerLeg; i++)
            {
                segments[j][i].radius = segmentRadiuses[i];
                //segments[i].mesh.transform.localScale = new Vector3(segmentRadiuses[i], segmentRadiuses[i], .5f);
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
    //         newMesh.transform.position = segments[i].pos;
    //         segments[i].mesh = newMesh;
    //     }
    // }
    
    #endregion Precomputed
    
    #region Looping
    
    // Update is called once per frame
    void Update()
    {
        if (!simulate) return;
        if (segments[0] == null)
            Debug.LogError($"leg named {gameObject.name} has no segments");

        // Different logic for IKEdit vs normal mode
        if (ikEditMode)
        {
            foreach (List<LegSegment> legSegments in segments)
                StartCoroutine(MoveFabrikIK(legSegments));
        }
        else
        {
            foreach (List<LegSegment> legSegments in segments)
            {
                if (legSegments[^1].ikMode)
                    StartCoroutine(MoveFabrikIK(legSegments));
                else MoveLegFK(legSegments, true, false);
            }
        }
    }
    
    IEnumerator MoveFabrikIK(List<LegSegment> legSegments)
    {
        bool isBaseAnchored = false;
        LegSegment pawSeg = legSegments[^1];
        
        pawSeg.ikMode = false;
        int legIndex = segments.IndexOf(legSegments);
        
        Vector3 startPawPos = pawSeg.pos;
        
        for (float timer = 0; timer < stepDuration; timer += Time.deltaTime)
        {
            Vector3 currentPos = Lerp(startPawPos, stepEndPositions[legIndex].position, timer /  stepDuration);
            legPaws[legIndex].position = currentPos;
            
            for (int depth = 0; depth < maxIKFabrikDepth; depth++)
            {
                MoveLegFK(legSegments, isBaseAnchored, true);
                isBaseAnchored = !isBaseAnchored;
            }

            yield return null;
        }
        
        legPaws[legIndex].position = stepEndPositions[legIndex].position;
        for (int depth = 0; depth < maxIKFabrikDepth; depth++)
        {
            MoveLegFK(legSegments, isBaseAnchored, true);
            isBaseAnchored = !isBaseAnchored;
        }

        pawSeg.takingStep = false;
    }

    /// <summary>
    /// Makes the leg follow one extremity (base or paw)
    /// </summary>
    void MoveLegFK(List<LegSegment> legSegments, bool isBaseAnchored, bool executingFabrik)
    {
        int legIndex = segments.IndexOf(legSegments);
        if (isBaseAnchored)
            legSegments[0].pos = legBases[legIndex].position;
        else
            legSegments[^1].pos = legPaws[legIndex].position;
        
        //DrawArrow.ForDebug(stepEndPositions[legIndex].position, legSegments[^1].pos - stepEndPositions[legIndex].position, Color.green);
        //DrawArrow.ForDebug(stepEndPositions[legIndex].position, stepBeginPositions[legIndex].position - stepEndPositions[legIndex].position, Color.green);
        
        // Steps condition
        List<LegSegment> adjacentLeg = legIndex % 2 == 0 ? segments[legIndex + 1] : segments[legIndex - 1];
        if (!executingFabrik && !adjacentLeg[^1].takingStep)
        {
            if (CheckForStep(legSegments)) return;
        }
        
        // Execute for each segment
        for (int i = isBaseAnchored ? 1 : segmentCountPerLeg - 2; isBaseAnchored ? i < segmentCountPerLeg: i >= 0; i += isBaseAnchored ? 1 : -1)
        {
            if (!executingFabrik && legSegments[i] == legSegments[^1])
                return;
            
            LegSegment parentSeg = legSegments[isBaseAnchored ? i - 1 : i + 1];
            
            // Circle constraint segment
            Vector3 posDiff = legSegments[i].pos - parentSeg.pos;
            Vector3 newPos = parentSeg.pos + posDiff.normalized * segmentDistance;
            
            // Clamp angle if necessary
            bool angleCheckCondition = isBaseAnchored ? i > 1 : i < segmentCountPerLeg - 2;
            if (angleCheckCondition)
            {
                ClampAngle(legSegments, isBaseAnchored, newPos, i);
            }
            
            legSegments[i].pos = newPos;
            
            
            //RotateMesh();
        }
    }

    void ClampAngle(List<LegSegment> legSegments, bool isBaseAnchored, Vector3 newPos, int i)
    {
        LegSegment parentSeg = legSegments[isBaseAnchored ? i - 1 : i + 1];
        LegSegment parentSeg2 = legSegments[isBaseAnchored ? i - 2 : i + 2];
                
        // Check neighboring segments and calculate angle
        Vector3 v1 = parentSeg2.pos - parentSeg.pos;
        Vector3 v2 = newPos - parentSeg.pos;
        float angle = Vector3.SignedAngle(v1, v2, Vector3.up);
                
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

    bool CheckForStep(List<LegSegment> legSegments)
    {
        int legIndex = segments.IndexOf(legSegments);
        
        // Instead of calculating angle, compare distance between endStepPos/pawPos and endStepPos/beginStepPos
        Vector3 pawSeg = legSegments[^1].pos;
        float pawDist = (pawSeg - stepEndPositions[legIndex].position).magnitude;
        float maxPawDist = (stepBeginPositions[legIndex].position - stepEndPositions[legIndex].position).magnitude;
            
        // If leg behind begin step pos, take a step towards end step pos
        //bool condition1 = legIndex % 2 == 0 ? rightStepCooldownTimer >= minStepCooldown : leftStepCooldownTimer >= minStepCooldown;
        if (pawDist > maxPawDist)
        {
            legSegments[^1].ikMode = true;
            legSegments[^1].takingStep = true;
            return true;
        }

        return false;
    }

    void RotateMesh()
    {
        //segments[i].mesh.transform.position = newPos;
            
        // Rotate segment mesh
        // float nextSegmentAngle = segments[i - 1].mesh.transform.eulerAngles.y;
        // Vector3 nextExtremity = Quaternion.AngleAxis(nextSegmentAngle - 90, Vector3.up) * Vector3.left * .5f + segments[i - 1].pos;
        // Vector3 lookAtPos = Lerp(segments[i - 1].pos, nextExtremity, lookAtExtremityFactor);
        //
        // Vector3 deltaPos = lookAtPos - newPos;
        // float meshAngle = Vector3.SignedAngle(Vector3.right, deltaPos, Vector3.up);
        // segments[i].mesh.transform.localEulerAngles = new Vector3(0,  meshAngle + 90, 0);
            
        // Scale to fill gaps
        // float angleDiff = Vector3.Angle(segments[i].mesh.transform.forward, segments[i - 1].mesh.transform.forward);
        // float scaleFactor = Mathf.Lerp(1, 1.35f, angleDiff / 45f);
        // segments[i].mesh.transform.localScale = new Vector3(segments[i].mesh.transform.localScale.x, segments[i].mesh.transform.localScale.y, .5f * scaleFactor);
    }
    
    #endregion Looping

    Vector3 Lerp(Vector3 v1, Vector3 v2, float t)
    {
        return new Vector3(v1.x + (v2.x - v1.x) * t, v1.y + (v2.y - v1.y) * t, v1.z + (v2.z - v1.z) * t);
    }

    void OnDrawGizmos()
    {
        if (!debugMode || !simulate) return;

        foreach (List<LegSegment> legSegments in segments)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(stepEndPositions[segments.IndexOf(legSegments)].position, .3f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(stepBeginPositions[segments.IndexOf(legSegments)].position, .2f);
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
    public bool ikMode;
    public bool takingStep;
}