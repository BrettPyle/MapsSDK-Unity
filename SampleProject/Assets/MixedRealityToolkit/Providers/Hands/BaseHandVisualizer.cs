﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    public class BaseHandVisualizer : MonoBehaviour, IMixedRealityHandVisualizer, IMixedRealitySourceStateHandler, IMixedRealityHandJointHandler, IMixedRealityHandMeshHandler
    {
        public virtual Handedness Handedness { get; set; }

        public GameObject GameObjectProxy => gameObject;

        public IMixedRealityController Controller { get; set; }

        protected readonly Dictionary<TrackedHandJoint, Transform> joints = new Dictionary<TrackedHandJoint, Transform>();
        protected MeshFilter handMeshFilter;

        private IMixedRealityInputSystem inputSystem = null;

        /// <summary>
        /// The active instance of the input system.
        /// </summary>
        protected IMixedRealityInputSystem InputSystem
        {
            get
            {
                if (inputSystem == null)
                {
                    MixedRealityServiceRegistry.TryGetService<IMixedRealityInputSystem>(out inputSystem);
                }
                return inputSystem;
            }
        }

        private void OnEnable()
        {
            InputSystem?.Register(gameObject);
        }

        private void OnDisable()
        {
            InputSystem?.Unregister(gameObject);
        }

        private void OnDestroy()
        {
            foreach (var joint in joints)
            {
                Destroy(joint.Value.gameObject);
            }

            if (handMeshFilter != null)
            {
                Destroy(handMeshFilter.gameObject);
            }
        }

        public bool TryGetJointTransform(TrackedHandJoint joint, out Transform jointTransform)
        {
            if (joints == null)
            {
                jointTransform = null;
                return false;
            }

            if (joints.TryGetValue(joint, out jointTransform))
            {
                return true;
            }

            jointTransform = null;
            return false;
        }

        void IMixedRealitySourceStateHandler.OnSourceDetected(SourceStateEventData eventData) { }

        void IMixedRealitySourceStateHandler.OnSourceLost(SourceStateEventData eventData)
        {
            if (Controller?.InputSource.SourceId == eventData.SourceId)
            {
                Destroy(gameObject);
            }
        }

        void IMixedRealityHandJointHandler.OnHandJointsUpdated(InputEventData<IDictionary<TrackedHandJoint, MixedRealityPose>> eventData)
        {
            if (eventData.Handedness != Controller?.ControllerHandedness)
            {
                return;
            }

            MixedRealityHandTrackingProfile handTrackingProfile = InputSystem?.InputSystemProfile.HandTrackingProfile;
            if (handTrackingProfile != null && !handTrackingProfile.EnableHandJointVisualization)
            {
                // clear existing joint GameObjects / meshes
                foreach (var joint in joints)
                {
                    Destroy(joint.Value.gameObject);
                }

                joints.Clear();
                return;
            }

            foreach (TrackedHandJoint handJoint in eventData.InputData.Keys)
            {
                Transform jointTransform;
                if (joints.TryGetValue(handJoint, out jointTransform))
                {
                    jointTransform.position = eventData.InputData[handJoint].Position;
                    jointTransform.rotation = eventData.InputData[handJoint].Rotation;
                }
                else
                {
                    GameObject prefab = InputSystem.InputSystemProfile.HandTrackingProfile.JointPrefab;
                    if (handJoint == TrackedHandJoint.Palm)
                    {
                        prefab = InputSystem.InputSystemProfile.HandTrackingProfile.PalmJointPrefab;
                    }
                    else if (handJoint == TrackedHandJoint.IndexTip)
                    {
                        prefab = InputSystem.InputSystemProfile.HandTrackingProfile.FingerTipPrefab;
                    }

                    GameObject jointObject;
                    if (prefab != null)
                    {
                        jointObject = Instantiate(prefab);
                    }
                    else
                    {
                        jointObject = new GameObject();
                    }

                    jointObject.name = handJoint.ToString() + " Proxy Transform";
                    jointObject.transform.position = eventData.InputData[handJoint].Position;
                    jointObject.transform.rotation = eventData.InputData[handJoint].Rotation;
                    jointObject.transform.parent = transform;

                    joints.Add(handJoint, jointObject.transform);
                }
            }
        }

        public void OnHandMeshUpdated(InputEventData<HandMeshInfo> eventData)
        {
            if (eventData.Handedness != Controller?.ControllerHandedness)
            {
                return;
            }

            if (handMeshFilter == null &&
                InputSystem?.InputSystemProfile?.HandTrackingProfile?.HandMeshPrefab != null)
            {
                handMeshFilter = Instantiate(InputSystem.InputSystemProfile.HandTrackingProfile.HandMeshPrefab).GetComponent<MeshFilter>();
            }

            if (handMeshFilter != null)
            {
                Mesh mesh = handMeshFilter.mesh;

                mesh.vertices = eventData.InputData.vertices;
                mesh.normals = eventData.InputData.normals;
                mesh.triangles = eventData.InputData.triangles;

                if (eventData.InputData.uvs != null && eventData.InputData.uvs.Length > 0)
                {
                    mesh.uv = eventData.InputData.uvs;
                }

                handMeshFilter.transform.position = eventData.InputData.position;
                handMeshFilter.transform.rotation = eventData.InputData.rotation;
            }
        }
    }
}