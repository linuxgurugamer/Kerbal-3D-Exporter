using System;
using UnityEngine;

namespace Kerbal_3D_Exporter
{
    public static class VesselDimensions
    {
        public struct Dimensions
        {
            public float Width;
            public float Height;
            public float Length;

            public Dimensions(float width, float height, float length)
            {
                Width = width;
                Height = height;
                Length = length;
            }

            public override string ToString()
            {
                return string.Format(
                    "Width: {0:F2} m, Height: {1:F2} m, Length: {2:F2} m",
                    Width,
                    Height,
                    Length);
            }
        }

        /// <summary>
        /// Gets the dimensions of the current craft.
        ///
        /// In the editor, this uses ShipConstruct.shipSize, which is used
        /// by the stock Engineer's Report.
        ///
        /// In flight, this calculates the dimensions from the active
        /// renderers on the vessel.
        /// </summary>
        public static bool TryGetCurrentDimensions(out Dimensions dimensions)
        {
            dimensions = new Dimensions();

            if (HighLogic.LoadedSceneIsEditor)
                return TryGetEditorDimensions(out dimensions);

            if (HighLogic.LoadedSceneIsFlight)
                return TryGetFlightDimensions(
                    FlightGlobals.ActiveVessel,
                    out dimensions);

            return false;
        }

        /// <summary>
        /// Gets the dimensions of the current editor craft using
        /// ShipConstruct.shipSize.
        /// </summary>
        public static bool TryGetEditorDimensions(out Dimensions dimensions)
        {
            dimensions = new Dimensions();

            if (!HighLogic.LoadedSceneIsEditor ||
                EditorLogic.fetch == null ||
                EditorLogic.fetch.ship == null ||
                EditorLogic.fetch.ship.parts == null ||
                EditorLogic.fetch.ship.parts.Count == 0)
            {
                return false;
            }

            Vector3 size = EditorLogic.fetch.ship.shipSize;

            dimensions = new Dimensions(
                size.x, // Width
                size.y, // Height
                size.z  // Length
            );

            return true;
        }

        /// <summary>
        /// Gets the dimensions of the active flight vessel from its
        /// enabled renderers.
        /// </summary>
        public static bool TryGetFlightDimensions(
            Vessel vessel,
            out Dimensions dimensions)
        {
            dimensions = new Dimensions();

            if (vessel == null ||
                !vessel.loaded ||
                vessel.parts == null ||
                vessel.parts.Count == 0)
            {
                return false;
            }

            Transform referenceTransform = vessel.ReferenceTransform;

            if (referenceTransform == null)
                return false;

            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;
            bool haveBounds = false;

            foreach (Part part in vessel.parts)
            {
                if (part == null)
                    continue;

                // includeInactive = false prevents inactive variant objects
                // from being included.
                Renderer[] renderers =
                    part.GetComponentsInChildren<Renderer>(false);

                foreach (Renderer renderer in renderers)
                {
                    if (!ShouldIncludeRenderer(renderer))
                        continue;

                    Bounds localBounds;
                    Transform boundsTransform;

                    SkinnedMeshRenderer skinnedRenderer =
                        renderer as SkinnedMeshRenderer;

                    if (skinnedRenderer != null)
                    {
                        localBounds = skinnedRenderer.localBounds;
                        boundsTransform = skinnedRenderer.transform;
                    }
                    else
                    {
                        MeshFilter meshFilter =
                            renderer.GetComponent<MeshFilter>();

                        if (meshFilter == null ||
                            meshFilter.sharedMesh == null)
                        {
                            continue;
                        }

                        localBounds = meshFilter.sharedMesh.bounds;
                        boundsTransform = meshFilter.transform;
                    }

                    AddBoundsCorners(
                        localBounds,
                        boundsTransform,
                        referenceTransform,
                        ref minimum,
                        ref maximum,
                        ref haveBounds);
                }
            }

            if (!haveBounds)
                return false;

            Vector3 size = maximum - minimum;

            /*
             * Vessel reference-transform axes:
             *
             * X = port/starboard width
             * Y = nose-to-tail length
             * Z = top-to-bottom height
             */
            dimensions = new Dimensions(
                size.x, // Width
                size.z, // Height
                size.y  // Length
            );

            return true;
        }

        private static bool ShouldIncludeRenderer(Renderer renderer)
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                return false;
            }

            // These effects should not affect the physical dimensions.
            if (renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer ||
                renderer is LineRenderer)
            {
                return false;
            }

            return true;
        }

        private static void AddBoundsCorners(
            Bounds bounds,
            Transform boundsTransform,
            Transform referenceTransform,
            ref Vector3 minimum,
            ref Vector3 maximum,
            ref bool haveBounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner =
                            center + Vector3.Scale(
                                extents,
                                new Vector3(x, y, z));

                        Vector3 worldCorner =
                            boundsTransform.TransformPoint(localCorner);

                        Vector3 vesselCorner =
                            referenceTransform.InverseTransformPoint(
                                worldCorner);

                        if (!haveBounds)
                        {
                            minimum = vesselCorner;
                            maximum = vesselCorner;
                            haveBounds = true;
                        }
                        else
                        {
                            minimum = Vector3.Min(minimum, vesselCorner);
                            maximum = Vector3.Max(maximum, vesselCorner);
                        }
                    }
                }
            }
        }


        public static float CalculateScaleForHeight(
            Dimensions dimensions,
            float desiredHeight,
            Utils.LengthUnit desiredHeightUnits)
        {
            if (dimensions.Height <= 0f)
                throw new ArgumentOutOfRangeException(
                    "dimensions",
                    "The craft height must be greater than zero.");

            if (desiredHeight <= 0f)
                throw new ArgumentOutOfRangeException(
                    "desiredHeight",
                    "The desired height must be greater than zero.");

            float desiredHeightMeters;

            switch (desiredHeightUnits)
            {
                case Utils.LengthUnit.Meters:
                    desiredHeightMeters = desiredHeight;
                    break;

                case Utils.LengthUnit.Inches:
                    desiredHeightMeters = desiredHeight * 0.0254f;
                    break;

                case Utils.LengthUnit.Millimeters:
                    desiredHeightMeters = desiredHeight * 0.001f;
                    break;

                case Utils.LengthUnit.Centimeters:
                    desiredHeightMeters = desiredHeight * 0.01f;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        "desiredHeightUnits",
                        "Unsupported length unit.");
            }

            return desiredHeightMeters / dimensions.Height;
        }



        public enum DimensionType
        {
            Width,
            Height,
            Length
        }

        public static float CalculateScaleForDimension(
            Dimensions dimensions,
            float desiredSize,
            Utils.LengthUnit desiredSizeUnits,
            DimensionType dimensionType)
        {
            if (desiredSize <= 0f)
                throw new ArgumentOutOfRangeException(
                    "desiredSize",
                    $"The desired size {desiredSize} must be greater than zero.");

            float currentSize;

            switch (dimensionType)
            {
                case DimensionType.Width:
                    currentSize = dimensions.Width;
                    break;

                case DimensionType.Height:
                    currentSize = dimensions.Height;
                    break;

                case DimensionType.Length:
                    currentSize = dimensions.Length;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        "dimensionType",
                        "Unsupported dimension type.");
            }

            if (currentSize <= 0f)
                throw new ArgumentOutOfRangeException(
                    "dimensions",
                    "The selected craft dimension must be greater than zero.");

            float desiredSizeMeters;

            switch (desiredSizeUnits)
            {
                case Utils.LengthUnit.Meters:
                    desiredSizeMeters = desiredSize;
                    break;

                case Utils.LengthUnit.Inches:
                    desiredSizeMeters = desiredSize * 0.0254f;
                    break;

                case Utils.LengthUnit.Millimeters:
                    desiredSizeMeters = desiredSize * 0.001f;
                    break;

                case Utils.LengthUnit.Centimeters:
                    desiredSizeMeters = desiredSize * 0.01f;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        "desiredSizeUnits",
                        "Unsupported length unit.");
            }

            return desiredSizeMeters / currentSize;
        }




    }
}