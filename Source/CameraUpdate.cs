//  ================================================================================
//  Real Solar System Visual Enhancements for Kerbal Space Program.
//
//  Copyright © 2016-2019, Alexander "Phineas Freak" Kampolis.
//
//  This file is part of Real Solar System Visual Enhancements.
//
//  Real Solar System Visual Enhancements is licensed under a Creative Commons Attribution-NonCommercial-ShareAlike 4.0
//  (CC-BY-NC-SA 4.0) license.
//
//  You should have received a copy of the license along with this work. If not, visit the official
//  Creative Commons web page:
//
//      • https://www.creativecommons.org/licensies/by-nc-sa/4.0
//
//  Based on code by Blackrack:
//
//      • https://github.com/LGhassen/Scatterer/blob/master/scatterer/Core.cs
//
//  Original is © Copyright Blackrack, GNU General Public License v3:
//
//      • https://www.gnu.org/licenses/gpl-3.0-standalone.html
//
//  Forum thread:
//
//      • https://forum.kerbalspaceprogram.com/index.php?showtopic=103963
//  ================================================================================

using System;
using UnityEngine;

namespace RSSVE
{
    /// <summary>
    /// ScaledSpace camera updater class. Operates only in the Flight scene.
    /// </summary>

    [KSPAddon (KSPAddon.Startup.Flight, false)]

    public class CameraUpdater : MonoBehaviour
    {
        /// <summary>
        /// The ScaledSpace camera object.
        /// </summary>

        public Camera SSCamera;

        /// <summary>
        /// The ScaledSpace camera near clip minimum value.
        /// </summary>

        const float fCameraNearClipLow = 1.0f;

        /// <summary>
        /// The ScaledSpace camera near clip maximum value.
        /// </summary>

        const float fCameraNearClipHigh = 100.0f;

        /// <summary>
        /// The ScaledSpace camera default near clip value.
        /// </summary>

        float fDefaultNearClip;

        /// <summary>
        /// The target body radius (in meters).
        /// </summary>

        float fTargetBodyRadius;

        /// <summary>
        /// The target body transform.
        /// </summary>

        Transform TargetBodyTransform;

        /// <summary>
        /// Method to start the ScaledSpace camera patcher.
        /// </summary>
        /// <returns>
        /// Does not return a value.
        /// </returns>

        void Start ()
        {
            try
            {
                // Before we start, check if the KSP version is compatible.

                if (CompatibilityChecker.IsCompatible ())
                {
                    // Get all available camera transforms.

                    Camera [] CameraList = Camera.allCameras;

                    Notification.Logger (Constants.AssemblyName, null, "Getting all available camera transforms...");

                    // Iterate until the SS camera transform is found.

                    for (int iCameraCount = 0; iCameraCount < CameraList.Length; iCameraCount++)
                    {
                        if (CameraList [iCameraCount].name.Equals ("Camera ScaledSpace"))
                        {
                            SSCamera = CameraList [iCameraCount];

                            Notification.Logger (Constants.AssemblyName, null, "Found the ScaledSpace camera transform!");

                            // Get the default SS camera near clip value. This ensures that even
                            // when the value is changed later, a fallback one will exist.

                            // This also ensures that custom near clip values (as set from
                            // other sources) will be maintained for every other view.

                            fDefaultNearClip = SSCamera.nearClipPlane;

                            Notification.Logger (Constants.AssemblyName, null, string.Format ("Default ScaledSpace camera near clip value: {0}", fDefaultNearClip));
                        }
                    }
                }
            }
            catch (Exception ExceptionStack)
            {
                Notification.Logger (Constants.AssemblyName, "Error", string.Format ("CameraUpdater.Start() caught an exception: {0},\n{1}\n", ExceptionStack.Message, ExceptionStack.StackTrace));
            }
            finally
            {
                Destroy (this);
            }
        }

        /// <summary>
        /// Method to update the ScaledSpace camera near clip value.
        /// </summary>
        /// <returns>
        /// Does not return a value.
        /// </returns>

        void Update ()
        {
            try
            {
                // Check if the KSP version is compatible.

                if (CompatibilityChecker.IsCompatible ())
                {
                    // Get the name of the body that the camera is currently focused to.

                    // FindNearestTarget() ensures that the camera target is actually a celestial body and
                    // NOT a vessel (bad things will happen if we try to get the radius of a vessel...).

                    string szTargetBodyName = PlanetariumCamera.fetch.FindNearestTarget ().name;

                    // Before we continue check if the body actually exists in the world.

                    if (!string.IsNullOrEmpty (szTargetBodyName))
                    {
                        // Get the transform of said body.

                        TargetBodyTransform = ScaledSpace.Instance.transform.Find (szTargetBodyName);

                        if (!TargetBodyTransform.Equals (null))
                        {
                            // Get the radius of said body.

                            fTargetBodyRadius = (float) FlightGlobals.GetBodyByName (szTargetBodyName).Radius;

                            // Skip the calculations if the body radius is less than 500 km (the near clip
                            // calculations do not return a correct value if the body radius is too small).

                            if (!fTargetBodyRadius.Equals (null) && fTargetBodyRadius > 500000)
                            {
                                // Set the cutoff altitude to be high enough so that the normalized
                                // near clip value will be small when near the body surface.

                                float fCameraCutoffAlt = 100 * fTargetBodyRadius;

                                // Calculate the distance between the body surface and the SS camera transforms.
                                // Take into account the body radius, since the transform of a celestial body is
                                // located exactly at the center of the sphere.

                                float fCameraDistance = Mathf.Abs (Vector3.Distance (ScaledSpace.ScaledToLocalSpace (SSCamera.transform.position),
                                                                                     ScaledSpace.ScaledToLocalSpace (TargetBodyTransform.transform.position)) - fTargetBodyRadius);

                                // Check if the near clip value actually needs patching (Z-flighting is almost
                                // invisible when the SS camera transform is high enough over the body surface).

                                if (fCameraDistance < fCameraCutoffAlt)
                                {
                                    // Calculate the ScaledSpace camera near clip value from the normalized body transform/camera distance.

                                    float fNearClipPlaneNormalized = fCameraNearClipHigh * (fCameraDistance / fCameraCutoffAlt);

                                    // Set the new near clip value. Make sure that we cannot set it to zero or to a stupidly large value.

                                    SSCamera.nearClipPlane = (float) Mathf.Round (Mathf.Clamp (fNearClipPlaneNormalized, fCameraNearClipLow, fCameraNearClipHigh));
                                }
                            }
                            else
                            {
                                // Set the near clip value back to it's default value.

                                SSCamera.nearClipPlane = fDefaultNearClip;
                            }
                        }
                    }

                    // Log some debugging information. NOTE: major log spam source!

                    if (Utilities.IsVerboseDebugEnabled)
                    {
                        Notification.Logger (Constants.AssemblyName, null, string.Format ("Target body name: {0}", szTargetBodyName));
                        Notification.Logger (Constants.AssemblyName, null, string.Format ("Target body transform: {0}", TargetBodyTransform));
                        Notification.Logger (Constants.AssemblyName, null, string.Format ("Target body radius: {0}", fTargetBodyRadius));
                        Notification.Logger (Constants.AssemblyName, null, string.Format ("SS camera near clip: {0}", SSCamera.nearClipPlane));
                    }
                }
            }
            catch (Exception ExceptionStack)
            {
                Notification.Logger (Constants.AssemblyName, "Error", string.Format ("CameraUpdater.Update() caught an exception: {0},\n{1}\n", ExceptionStack.Message, ExceptionStack.StackTrace));
            }
        }
    }
}
