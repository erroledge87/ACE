using System;
using System.Numerics;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Collision;
using ACE.Server.Physics.Common;

namespace ACE.Server.Physics
{
    /// <summary>
    /// Spherical collision detection
    /// </summary>
    public class Sphere
    {
        /// <summary>
        /// The center point of the sphere
        /// </summary>
        public Vector3 Center;

        /// <summary>
        /// The radius of the sphere
        /// </summary>
        public float Radius;

        /// <summary>
        /// Default constructor
        /// </summary>
        public Sphere()
        {
        }

        /// <summary>
        /// Constructs a sphere from a center point and radius
        /// </summary>
        /// <param name="center">The center point of the sphere</param>
        /// <param name="radius">The radius of the sphere</param>
        public Sphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>
        /// Redirects a sphere to be on collision course towards a point
        /// </summary>
        /// <param name="obj">If set to PerfectClip, does a more precise check</param>
        /// <param name="path">The path information for the sphere</param>
        /// <param name="collisions">The collision information for the sphere</param>
        /// <param name="checkPos">The spherical point to redirect towards</param>
        /// <param name="disp">Currently doesn't seem to be used?</param>
        /// <param name="radsum">The sum of the sphere and spherical point radii</param>
        /// <param name="sphereNum">Used as an offset in path.GlobalCurrCenter to determine movement</param>
        /// <returns>The TransitionState either collided or adjusted</returns>
        public TransitionState CollideWithPoint(ObjectInfo obj, SpherePath path, CollisionInfo collisions, Sphere checkPos, Vector3 disp, float radsum, int sphereNum)
        {
            var gCenter = path.GlobalCurrCenter[sphereNum];
            var globalOffset = gCenter - Center;

            if (obj.State.HasFlag(ObjectInfoState.PerfectClip))
            {
                var blockOffset = LandDefs.GetBlockOffset(path.CurPos.ObjCellID, path.CheckPos.ObjCellID);
                var checkOffset = checkPos.Center - gCenter + blockOffset;
                var collisionTime = FindTimeOfCollision(checkOffset, globalOffset, radsum + PhysicsGlobals.EPSILON);
                if (collisionTime > PhysicsGlobals.EPSILON || collisionTime < -1.0f)
                    return TransitionState.Collided;
                else
                {
                    var collisionOffset = checkOffset * (float)collisionTime;
                    var old_disp = collisionOffset + checkOffset - Center;       // ?? verify
                    var invRad = 1.0f / radsum;
                    var collision_normal = old_disp * invRad;
                    collisions.SetCollisionNormal(collision_normal);
                    path.AddOffsetToCheckPos(old_disp, checkPos.Radius);
                    return TransitionState.Adjusted;
                }
            }
            else
            {
                if (!CollisionInfo.NormalizeCheckSmall(ref globalOffset))
                    collisions.SetCollisionNormal(globalOffset);

                return TransitionState.Collided;
            }
        }

        /// <summary>
        /// Collision detection from legacy implementation
        /// </summary>
        /// <returns></returns>
        public static bool CollidesWithSphere(Vector3 otherSphere, float diameter)
        {
            // original implementation with FPU flag
            // perform inline, possibly replacing the calls to Sphere.Intersect()
            return false;
        }

        /// <summary>
        /// Finds the percentage along this sphere's movement path
        ///  if/ when it collides with another sphere
        /// </summary>
        /// <param name="movement">The movement vector for the current sphere</param>
        /// <param name="spherePos">The position of the other sphere</param>
        /// <param name="radSum">The sum of the radii between this sphere and the other sphere</param>
        /// <returns>A 0-1 interval along the movement path for the time of collision, or -1 non-collision</returns>
        /// <remarks>Verify this could be different from original AC, which seems to return a negative interval?</remarks>
        public double FindTimeOfCollision(Vector3 movement, Vector3 spherePos, float radSum)
        {
            var distSq = movement.LengthSquared();
            if (distSq < PhysicsGlobals.EPSILON) return -1;

            var nonCollide = spherePos.LengthSquared() - radSum * radSum;
            if (nonCollide < PhysicsGlobals.EPSILON) return -1;

            var cDistSq = -(spherePos.X * movement.X + spherePos.Y * movement.Y + spherePos.Z * movement.Z);
            var nonCollideB = cDistSq * cDistSq - nonCollide * distSq;
            if (nonCollideB < 0) return -1;

            var cDist = Math.Sqrt(nonCollideB);
            if (cDistSq - cDist < 0)
                return -1 * (cDist - (spherePos.X * movement.X + spherePos.Y * movement.Y + spherePos.Z * movement.Z)) / distSq;
            else
                return -1 * (cDistSq - cDist) / distSq;
        }

        /// <summary>
        /// Returns true if this sphere intersects with another sphere
        /// </summary>
        public bool Intersects(Sphere sphere)
        {
            var delta = sphere.Center - Center;
            var radSum = Radius + sphere.Radius;
            return delta.LengthSquared() < radSum * radSum;
        }

        /// <summary>
        /// Determines if this sphere collides with any other spheres during its transitions
        /// </summary>
        public TransitionState IntersectsSphere(Position position, float scale, Transition transition, bool isCreature)
        {
            var globalPos = position.LocalToGlobal(transition.SpherePath.CheckPos, position, Center * scale);
            return new Sphere(globalPos, Radius * scale).IntersectsSphere(transition, isCreature);    // check scaling
        }

        /// <summary>
        /// Determines if this sphere collides with anything during its transition
        /// </summary>
        /// <param name="transition">The transition path for this sphere</param>
        /// <param name="isCreature">Flag indicates if this sphere is a player / monster</param>
        /// <returns>The collision result for this transition path</returns>
        public TransitionState IntersectsSphere(Transition transition, bool isCreature)
        {
            var globSphere = transition.SpherePath.GlobalSphere[0];
            var dx = globSphere.Center.X - Center.X;
            var dy = globSphere.Center.Y - Center.Y;
            var dz = globSphere.Center.Z - Center.Z;
            var radsum = globSphere.Radius + Radius - PhysicsGlobals.EPSILON;   // set transition info?

            var disp = new Vector3(globSphere.Center.X - Center.X, globSphere.Center.Y - Center.Y, globSphere.Center.Z - Center.Z);     // offset 1?

            if (transition.SpherePath.ObstructionEthereal || transition.SpherePath.CheckPos.ObjCellID == 1)   // verify last bit
            {
                if (dx * dx + dy * dy + dz * dz <= radsum)
                    return TransitionState.Collided;

                if (transition.SpherePath.NumSphere > 1)
                {
                    if (CollidesWithSphere(disp, radsum))
                        return TransitionState.Collided;
                    else
                        return TransitionState.OK;
                }
                return TransitionState.OK;
            }

            if (transition.SpherePath.CheckPos.Frame == null)   // verify these offsets, 86
            {
                if (transition.SpherePath.BackupCheckPos != null)   // offset 118
                {
                    if (CollidesWithSphere(disp, radsum))
                    {
                        if (transition.SpherePath.NumSphere > 1)
                        {
                            var sDiff = globSphere.Center - Center;     // take contact plane into account
                            if (CollidesWithSphere(sDiff, radsum))
                                return TransitionState.Collided;
                            else
                                return TransitionState.OK;
                        }
                        return TransitionState.OK;
                    }
                    return TransitionState.Collided;
                }
                if (transition.SpherePath.StepUp /*transition.SpherePath.CurPos != null*/)   // offset 64
                {
                    if (transition.ObjectInfo.State.HasFlag(ObjectInfoState.Contact) ||
                        transition.ObjectInfo.State.HasFlag(ObjectInfoState.OnWalkable))
                    {
                        if (CollidesWithSphere(disp, radsum))
                            return StepSphereUp(transition, globSphere, disp, radsum);      // check last param

                        if (transition.SpherePath.NumSphere > 1)
                        {
                            var sDiff = globSphere.Center - Center;     // middle param 0
                            if (CollidesWithSphere(sDiff, radsum))
                                return SlideSphere(transition.ObjectInfo, transition.SpherePath, transition.CollisionInfo, globSphere, sDiff, radsum, 0);
                        }
                    }
                    else if (transition.ObjectInfo.State.HasFlag(ObjectInfoState.PathClipped))
                    {
                        if (CollidesWithSphere(disp, radsum))
                            return CollideWithPoint(transition.ObjectInfo, transition.SpherePath, null, globSphere, disp, radsum, 0);
                    }
                    else
                    {
                        if (CollidesWithSphere(disp, radsum))
                            return LandOnSphere(transition.ObjectInfo, transition.SpherePath, null, globSphere, disp, radsum, 0);

                        if (transition.SpherePath.NumSphere > 1)
                        {
                            var sDiff = globSphere.Center - Center;
                            if (CollidesWithSphere(sDiff, radsum))
                                return CollideWithPoint(transition.ObjectInfo, transition.SpherePath, null, globSphere, disp, radsum, 0);
                        }
                    }
                    return TransitionState.OK;
                }

                if (isCreature)
                    return TransitionState.OK;

                if (CollidesWithSphere(disp, radsum) || transition.SpherePath.NumSphere > 1)
                {
                    var sDiff = globSphere.Center - Center;     // middle param 0
                    if (CollidesWithSphere(sDiff, radsum))
                    {
                        var curPos = transition.SpherePath.GetCurPosCheckPosBlockOffset();
                        var pathDiff = transition.SpherePath.GlobalCurrCenter[0] - globSphere.Center;  // offset 14
                        var delta = pathDiff - curPos;
                        radsum += PhysicsGlobals.EPSILON;
                        var sqLen = delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z;
                        var b = -(dx * delta.X * + dy * delta.Y * + dz * delta.Z);
                        if (Math.Abs(sqLen) >= PhysicsGlobals.EPSILON)
                        {
                            var c = Math.Sqrt(b * b - (dx * dx + dy * dy + dz * dz - radsum * radsum) * sqLen) - sqLen;
                            if (c > 1)
                                c = b * 2 - c;
                            var d = c / sqLen;
                            var timecheck = (1 - d) * transition.SpherePath.WalkableAllowance;     // offset 111
                            if (timecheck < transition.SpherePath.WalkableAllowance && timecheck >= -0.1f)
                            {
                                var dPathDiff = new Vector3((float)(pathDiff.X * d), (float)(pathDiff.Y * d), (float)(pathDiff.Z * d)) / radsum;
                                if (transition.SpherePath.IsWalkableAllowable((float)(pathDiff.Z * d)))
                                {
                                    var contact_plane = new Plane();    // determined above
                                    var f = Plane.Transform(contact_plane, Matrix4x4.CreateScale(globSphere.Radius));
                                    var collisionPlane = new Plane(globSphere.Center.X - f.Normal.X, globSphere.Center.Y - f.Normal.Y, globSphere.Center.Z - f.Normal.Z, f.D);
                                    transition.CollisionInfo.SetContactPlane(collisionPlane, true);
                                    transition.CollisionInfo.ContactPlaneCellID = transition.SpherePath.CurPos.ObjCellID;
                                    transition.SpherePath.WalkableAllowance = (float)timecheck;     // check offset 111
                                    transition.SpherePath.AddOffsetToCheckPos(dPathDiff, globSphere.Radius);
                                    return TransitionState.Adjusted;
                                }
                                return TransitionState.OK;
                            }
                        }
                        return TransitionState.Collided;
                    }
                }
                return TransitionState.OK;
            }

            if (!isCreature)
                return StepSphereDown(transition.ObjectInfo, transition.SpherePath, transition.CollisionInfo, globSphere, disp, radsum);

            return TransitionState.OK;
        }

        /// <summary>
        /// Handles the collision when an object lands on a sphere
        /// </summary>
        public TransitionState LandOnSphere(ObjectInfo obj, SpherePath path, CollisionInfo collisions, Sphere checkPos, Vector3 disp, float radsum, int sphereNum)
        {
            var collisionNormal = new Vector3(path.GlobalCurrCenter[0].X - Center.X, path.GlobalCurrCenter[0].Y - Center.Y, path.GlobalCurrCenter[0].Z - Center.Z);
            if (CollisionInfo.NormalizeCheckSmall(ref collisionNormal))
                return TransitionState.Collided;
            else
            {
                path.SetCollide(collisionNormal);
                path.WalkableAllowance = CollisionInfo.LandingZ;
                return TransitionState.Adjusted;
            }
        }

        /// <summary>
        /// Attempts to slide the sphere from a collision
        /// </summary>
        public TransitionState SlideSphere(ObjectInfo obj, SpherePath path, CollisionInfo collisions, Vector3 disp, float radsum, int sphereNum)
        {
            var globSphere = path.GlobalSphere[sphereNum];
            var center = path.GlobalCurrCenter[sphereNum];
            var collisionNormal = new Vector3(center.X - Center.X, center.Y - Center.Y, center.Z - Center.Z);
            if (CollisionInfo.NormalizeCheckSmall(ref collisionNormal))
                return TransitionState.Collided;

            collisions.SetCollisionNormal(collisionNormal);

            var contactPlane = collisions.ContactPlaneValid ? collisions.ContactPlane : collisions.LastKnownContactPlane;

            var direction = Vector3.Cross(contactPlane.Normal, collisionNormal);
            var blockOffset = LandDefs.GetBlockOffset(path.CurPos.ObjCellID, path.CheckPos.ObjCellID);
            var diffOffset = globSphere.Center - Center;
            var globOffset = blockOffset + diffOffset;
            var dirLenSq = direction.LengthSquared();
            if (dirLenSq >= PhysicsGlobals.EPSILON)
            {
                var globOffsetDir = globOffset * direction;
                var skid_dir = direction * globOffsetDir;
                // TODO
                // what is v21 | v22 referencing?
                return TransitionState.Collided;
            }
            if (disp.X * contactPlane.Normal.X + disp.Y * contactPlane.Normal.Y + disp.Z * contactPlane.Normal.Z < 0.0f)
                return TransitionState.Collided;

            path.AddOffsetToCheckPos(direction, globSphere.Radius);
            return TransitionState.Slid;
        }

        /// <summary>
        /// Attempts to slide a sphere from a collision
        /// </summary>
        public TransitionState SlideSphere(ObjectInfo obj, SpherePath path, CollisionInfo collisions, Sphere checkPos, Vector3 disp, float radsum, int sphereNum)
        {
            var center = path.GlobalCurrCenter[sphereNum];
            var collisionNormal = new Vector3(center.X - Center.X, center.Y - Center.Y, center.Z - Center.Z);
            if (CollisionInfo.NormalizeCheckSmall(ref collisionNormal))
                return TransitionState.Collided;
            else
                return checkPos.SlideSphere(path, collisions, collisionNormal, center);
        }

        /// <summary>
        /// Attempts to slide a sphere from a collision
        /// </summary>
        public TransitionState SlideSphere(SpherePath path, CollisionInfo collisions, Vector3 collisionNormal, Vector3 currPos)
        {
            if (collisionNormal.Equals(Vector3.Zero))
            {
                var deltaPos = new Vector3(currPos.X - Center.X, currPos.Y - Center.Y, currPos.Z - Center.Z);
                var hDeltaPos = deltaPos * 0.5f;
                path.AddOffsetToCheckPos(hDeltaPos, Radius);
                return TransitionState.Adjusted;
            }
            collisions.SetCollisionNormal(collisionNormal);
            var offset = LandDefs.GetBlockOffset(path.CurPos.ObjCellID, path.CheckPos.ObjCellID);
            var rDeltaPos = new Vector3(Center.X - currPos.X, Center.Y - currPos.Y, Center.Z - currPos.Z);
            var contactPlane = new Plane();
            if (!collisions.ContactPlaneValid)
                contactPlane = collisions.LastKnownContactPlane;
            var direction = Vector3.Cross(collisionNormal, contactPlane.Normal);
            var cross1 = collisionNormal.Y * contactPlane.Normal.Z - collisionNormal.Z * contactPlane.Normal.Y;
            var cross2 = collisionNormal.Z * contactPlane.Normal.X - collisionNormal.X * contactPlane.Normal.Z;
            var cross3 = collisionNormal.X * contactPlane.Normal.Y - collisionNormal.Y * contactPlane.Normal.X;
            var dirLenSq = direction.LengthSquared();
            if (dirLenSq >= PhysicsGlobals.EPSILON)
            {
                var crossDelta = rDeltaPos * direction;
                var invDirLenSq = 1.0f / dirLenSq;
                var c = crossDelta * invDirLenSq;   // verification
                if (c.X * c.X + c.Y * c.Y + c.Z * c.Z < PhysicsGlobals.EPSILON)
                    return TransitionState.Collided;

                path.AddOffsetToCheckPos(c, Radius);
                return TransitionState.Slid;
            }
            if (collisionNormal.X * contactPlane.Normal.X + collisionNormal.Y * contactPlane.Normal.Y + collisionNormal.Z * contactPlane.Normal.Z >= 0.0f)
            {
                var pathb = collisionNormal * rDeltaPos * -collisionNormal;     // -z?
                path.AddOffsetToCheckPos(pathb, Radius);
                return TransitionState.Slid;
            }

            var nCollisionNormal = collisionNormal * -1;
            if (CollisionInfo.NormalizeCheckSmall(ref nCollisionNormal))
                collisions.SetCollisionNormal(nCollisionNormal);

            return TransitionState.Collided;
        }

        /// <summary>
        /// Attempts to move the sphere down from a collision
        /// </summary>
        public TransitionState StepSphereDown(ObjectInfo obj, SpherePath path, CollisionInfo collisions, Sphere checkPos, Vector3 disp, float radsum)
        {
            if (disp.X * disp.X + disp.Y * disp.Y + disp.Z * disp.Z > radsum * radsum)
            {
                if (path.NumSphere <= 1)
                    return TransitionState.OK;

                var globSphere = path.GlobalSphere[1];
                var globOffset = new Vector3(globSphere.Center.X - Center.X, globSphere.Center.Y - Center.Y, globSphere.Center.Z - Center.Z);
                if (!CollidesWithSphere(globOffset, radsum))
                    return TransitionState.OK;
            }

            var stepDown = path.StepDownAmt * path.WalkInterp;
            if (Math.Abs(stepDown) < PhysicsGlobals.EPSILON)
                return TransitionState.Collided;

            var scaledStep = (Math.Sqrt(radsum * radsum - (disp.X * disp.X + disp.Y * disp.Y)) - disp.Z) / stepDown;
            var timecheck = -scaledStep * path.WalkInterp;
            if (timecheck >= path.WalkInterp || timecheck < -0.1f)
                return TransitionState.Collided;

            var invRadSum = 1.0f / radsum;
            var _disp = stepDown * scaledStep;
            var amount = _disp * invRadSum;
            if (amount <= path.WalkableAllowance)
                return TransitionState.OK;

            var scaled = _disp * Radius;
            var dSphere = new Vector3((float)(Center.X + scaled), (float)(Center.Y + scaled), (float)(Center.Z + scaled));
            var restPlane = new Plane(dSphere, (float)_disp);
            collisions.SetContactPlane(restPlane, true);
            collisions.ContactPlaneCellID = path.CheckPos.ObjCellID;
            path.WalkInterp = (float)timecheck;
            var dz = new Vector3(0, 0, (float)_disp);
            path.AddOffsetToCheckPos(dz, checkPos.Radius);
            return TransitionState.Adjusted;
        }

        /// <summary>
        /// Attempts to move the sphere up from a collision
        /// </summary>
        public TransitionState StepSphereUp(Transition transition, Sphere checkPos, Vector3 disp, float radsum)
        {
            radsum += PhysicsGlobals.EPSILON;
            if (transition.ObjectInfo.StepUpHeight < radsum - disp.Z)
                return SlideSphere(transition.ObjectInfo, transition.SpherePath, transition.CollisionInfo, disp, radsum, 0);
            else
            {
                var globCenter = transition.SpherePath.GlobalCurrCenter[0];
                var collisionNormal = globCenter - Center;
                if (transition.StepUp(collisionNormal))
                    return TransitionState.OK;
                else
                    return transition.SpherePath.StepUpSlide(transition.ObjectInfo, transition.CollisionInfo);
            }
        }

        /// <summary>
        /// Detects if a ray intersects with a sphere
        /// </summary>
        /// <param name="ray">A ray is defined by a start point, a unit direction, and a length</param>
        /// <param name="timeOfIntersection">out parameter, the length of the ray when it hit the sphere</param>
        /// <returns>True if ray intersected, otherwise false.</returns>
        /// <remarks>
        /// - If the start point of the ray inside sphere, it is not considered an intersection.
        /// - If the sphere is behind the ray start point, with the ray direction pointing away from the sphere,
        ///   it can still return true for intersection, with timeOfIntersection as a negative value.
        /// </remarks>
        public bool SphereIntersectsRay(Ray ray, out double timeOfIntersection)
        {
            timeOfIntersection = 0;

            var distSq = ray.Dir.LengthSquared();
            if (distSq < PhysicsGlobals.EPSILON) return false;      // dir should be unit vector, redundant?

            // detect intersection
            var delta = ray.Point - Center;
            var c = delta.LengthSquared() - Radius * Radius;
            if (c <= 0) return false;

            // detect point of intersection
            var b = -(delta.X * ray.Dir.X + delta.Y * ray.Dir.Y + delta.Z * ray.Dir.Z);
            var d = b * b - c * distSq;
            if (d < 0) return false;

            var dist = Math.Sqrt(d);
            if (b <= dist)
                timeOfIntersection = (b + dist) / distSq;
            else
                timeOfIntersection = (b - dist) / distSq;

            return true;
        }
    }
}