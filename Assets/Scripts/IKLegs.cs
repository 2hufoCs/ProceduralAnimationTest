using UnityEngine;
using System.Collections.Generic;
using NaughtyAttributes;
using System.Linq;
using System.Collections;
using UnityEngine.Splines;
using System;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine.UIElements;

[ExecuteAlways]
public class IKLegs : MonoBehaviour
{
    [SerializeField] private bool simulate = true;
    [SerializeField] private bool ikEditMode;

    [Header("Legs")] 
    [OnValueChanged(nameof(RecomputeLegs))] public int legCount;
    [SerializeField, UnityEngine.Range(1, 100), OnValueChanged(nameof(RecomputeLegs))] private int segmentCountPerLeg;

    [SerializeField] private List<bool> legDirections = new();
    [SerializeField] private List<Transform> stepEndPositions = new();
    [SerializeField] private List<Transform> stepBeginPositions = new();
    [SerializeField] private int maxIKFabrikDepth;
    
    [Header("Segments")]
    [SerializeField, OnValueChanged(nameof(RecomputeLegs))] private float segmentDistance;
    [SerializeField] private float maxSegmentAngle;
    [SerializeField, UnityEngine.Range(0, 1)] private float lookAtExtremityFactor;
    
    [SerializeField, OnValueChanged(nameof(ReassignSegmentRadiuses))] private List<float> segmentRadiuses = new();
    public List<Transform> legBases = new();
    [SerializeField] private List<Transform> legPaws = new();
    private float baseRadius = .2f;

    public float stepDuration;

    [Header("Meshes")] 
    [SerializeField] private Transform legMeshParent;
    [SerializeField] private bool hasHumanoidLegs;

    [SerializeField, ShowIf(nameof(hasHumanoidLegs))] private GameObject upperArmMesh;
    [SerializeField, ShowIf(nameof(hasHumanoidLegs))] private GameObject forearmMesh;

    [SerializeField, HideIf(nameof(hasHumanoidLegs))] private GameObject legSegmentMesh;
    [SerializeField] private GameObject pawMesh;
    [SerializeField] private GameObject elbowMesh;


    [Header("Debug")] 
    [SerializeField] private bool debugMode;
    [SerializeField] private Color segmentColor;

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

        ReconstructMeshes();
        ReassignSegmentRadiuses();
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
    
    [Button]
    void ReconstructMeshes()
    {
        // Destroy previous meshes (even in editor)
        var tempList = legMeshParent.Cast<Transform>().ToList();
        foreach(var child in tempList)
        {
            foreach (var subChild in child.Cast<Transform>().ToList())
                DestroyImmediate(subChild.gameObject);
            DestroyImmediate(child.gameObject);
        }
        
        // Spawn new meshes
        for (int j = 0; j < legCount; j++)
        {
            // Fun way to spawn an empty object instead of using Instantiate
            GameObject newLeg = new GameObject
            {
                transform =
                {
                    position = segments[j][0].pos,
                    parent = legMeshParent
                },
                name = "Leg" + j
            };

            Transform parent = newLeg.transform;
            
            // Specific spawn for humanoid legs
            if (hasHumanoidLegs)
            {
                if (segmentCountPerLeg != 3)
                    Debug.LogError($"entity named {gameObject.name} has humanoid legs, yet it has {segmentCountPerLeg} segments per leg");
                
                InitializeLegMesh(elbowMesh, segments[j][0].pos, Quaternion.identity, "Shoulder", parent, new Vector2Int(j, 0));
                GameObject upperArm = InitializeLegMesh(upperArmMesh, (segments[j][0].pos + segments[j][1].pos) / 2, Quaternion.identity, "UpperArm", parent, new Vector2Int(j, -1));
                InitializeLegMesh(elbowMesh, segments[j][1].pos, Quaternion.identity, "Elbow", parent, new Vector2Int(j, 1));
                GameObject forearm = InitializeLegMesh(forearmMesh, (segments[j][1].pos + segments[j][2].pos) / 2, Quaternion.identity, "Forearm", parent, new Vector2Int(j, -1));
                InitializeLegMesh(pawMesh, segments[j][2].pos, Quaternion.identity, "Paw", parent, new Vector2Int(j, 2));

                segments[j][0].inBetweenMesh = upperArm;
                segments[j][1].inBetweenMesh = forearm;
                continue;
            }
            
            for (int i = 0; i < segmentCountPerLeg - 1; i++)
            {
                InitializeLegMesh(legSegmentMesh, segments[j][i].pos, Quaternion.identity, $"LegElbow{i}", parent, new Vector2Int(j, i));
                GameObject segment = InitializeLegMesh(legSegmentMesh, (segments[j][i].pos + segments[j][i + 1].pos) / 2, Quaternion.identity, $"LegSegment{i}", parent, new Vector2Int(j,-1));
                segments[j][i].inBetweenMesh = segment;
            }
            // Finally, add paw
            InitializeLegMesh(pawMesh, segments[j][segmentCountPerLeg - 1].pos, Quaternion.identity, $"LegPaw", parent, new Vector2Int(j, segmentCountPerLeg - 1));
        }
    }

    GameObject InitializeLegMesh(GameObject prefab, Vector3 pos, Quaternion rot, string legName, Transform parent, Vector2Int segIndex)
    {
        GameObject legPart = Instantiate(prefab, pos, rot, legMeshParent);
        legPart.name = legName;
        legPart.transform.parent = parent;
        
        if (segIndex.x < 0 || segIndex.y < 0) return legPart;
        segments[segIndex.x][segIndex.y].mesh = legPart;
        return legPart;
    }
    
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
        List<LegSegment> nextLeg = legIndex % 4 <= 1 && legIndex < legCount - 2
            ? segments[legIndex + 2]
            : segments[legIndex - 2];
        if (!executingFabrik && !adjacentLeg[^1].takingStep && !nextLeg[^1].takingStep)
        {
            if (CheckForStep(legSegments)) return;
        }
        
        // Execute for each segment
        for (int i = isBaseAnchored ? 1 : segmentCountPerLeg - 2; isBaseAnchored ? i < segmentCountPerLeg: i >= 0; i += isBaseAnchored ? 1 : -1)
        {
            LegSegment parentSeg = legSegments[isBaseAnchored ? i - 1 : i + 1];
            
            // Circle constraint segment
            Vector3 posDiff = legSegments[i].pos - parentSeg.pos;
            
            // if (!executingFabrik && legSegments[i] == legSegments[^1] && posDiff.magnitude < segmentDistance + .01f)
            //     return;
            
            Vector3 newPos = parentSeg.pos + posDiff.normalized * segmentDistance;
            
            // Clamp angle if necessary
            bool angleCheckCondition = isBaseAnchored ? i > 1 : i < segmentCountPerLeg - 2;
            if (angleCheckCondition)
                ClampAngle(legSegments, isBaseAnchored, newPos, i);
            
            legSegments[i].pos = newPos;
/*
            if (!executingFabrik)
                RotateMesh(legIndex, i);
*/
        }
        
        // Don't rotate while executing IK

        if (executingFabrik) return;
        for (int i = 0; i < segmentCountPerLeg; i++)
        {
            RotateMesh(legIndex, i);
        }

    }

    float ClampAngle(List<LegSegment> legSegments, bool isBaseAnchored, Vector3 newPos, int i)
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

        return angle;
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

    void RotateMesh(int legIndex, int segmentIndex)
    {
        LegSegment segment = segments[legIndex][segmentIndex];
        if (segment.mesh)
            segment.mesh.transform.position = segment.pos;
        
        // In this case, it's a paw, so don't apply in between mesh logic
        if (segmentIndex == segmentCountPerLeg - 1)
        {
            segment.mesh.transform.localEulerAngles = segments[legIndex][segmentIndex - 1].mesh.transform.eulerAngles;
            return;
        }
        
        LegSegment nextSegment = segments[legIndex][segmentIndex + 1];

        if (!segment.inBetweenMesh)
            return;
            
        segment.inBetweenMesh.transform.position = (segment.pos + nextSegment.pos) / 2;
            
         // Rotate in between pos
         Vector3 lookAtPos = Lerp(nextSegment.pos, segment.inBetweenMesh.transform.position, lookAtExtremityFactor);
        
         Vector3 deltaPos = lookAtPos - segment.inBetweenMesh.transform.position;
         DrawArrow.ForDebug(segment.inBetweenMesh.transform.position + Vector3.up, deltaPos + Vector3.up, Color.crimson);
         
         float meshAngle = Vector3.SignedAngle(Vector3.right, deltaPos, Vector3.up);
         segment.inBetweenMesh.transform.localEulerAngles = new Vector3(hasHumanoidLegs ? -90 : 0,  meshAngle, 0);
         if (segment.mesh) segment.mesh.transform.localEulerAngles = segment.inBetweenMesh.transform.localEulerAngles;
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
    public GameObject mesh;
    public GameObject inBetweenMesh;
    
    public bool forward;
    public bool mirrored;
    public bool ikMode;
    public bool takingStep;
}