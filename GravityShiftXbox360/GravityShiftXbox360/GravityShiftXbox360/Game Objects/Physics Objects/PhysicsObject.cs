﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;
using GravityShift.Import_Code;
using GravityShift.MISC_Code;
using GravityShift.Game_Objects.Static_Objects;

namespace GravityShift
{
    /// <summary>
    /// Represents an object that has rules based on physics
    /// </summary>
    abstract class PhysicsObject : GameObject
    {
        protected PhysicsEnvironment mEnvironment;
        public PhysicsEnvironment Environment
        {
            get { return mEnvironment; }
            set { mEnvironment = value; }
        }

        protected float mMass;
        public float Mass
        {
            get { return mMass; }
            set { mMass = value; }
        }

        private bool mIsRail;
        public bool IsRail { get { return mIsRail; } }

        //only used for rails
        private Vector2 mOriginalPosition;
        private float mHiBound;
        private float mLowBound;
        private float mAxis;
        
        //All forces applied to this physicsObject
        private Vector2 mGravityForce = new Vector2(0,0);
        private Vector2 mResistiveForce = new Vector2(1,1);
        private Vector2 mAdditionalForces = new Vector2(0, 0);

        /// <summary>
        /// number of pixels that the player can intersect the hazard without dying
        /// </summary>
        private const float HAZARDFORGIVENESS = 12.0f;

        /// <summary>
        /// Directional force on this object
        /// </summary>
        public Vector2 TotalForce
        {
            get {  return (Vector2.Add(mGravityForce,mAdditionalForces));  }
        }

        /// <summary>
        /// Speed and direction of this object
        /// </summary>
        public Vector2 ObjectVelocity
        {
            get { return mVelocity;  }
            set { mVelocity = value; }
        }

        /// <summary>
        /// Constructs a PhysicsObject; Loads the required info from the content pipeline, and defines its size and location
        /// </summary>
        /// <param name="content">Content pipeline</param>
        /// <param name="spriteBatch">The drawing canvas of the game. Used for collision detection with the level</param>
        /// <param name="name">Name of the physics object so that it can be loaded</param>
        /// <param name="scalingFactors">Factor for the image resource(i.e. half the size would be (.5,.5)</param>
        /// <param name="initialPosition">Position of where this object starts in the level</param>
        public PhysicsObject(ContentManager content, ref PhysicsEnvironment environment, float friction, EntityInfo entity)
            :base(content, friction, entity)
        {
            mEnvironment = environment;
            mVelocity = new Vector2(0, 0);
            mOriginalPosition = mOriginalInfo.mLocation;
            mIsRail = mOriginalInfo.mProperties.ContainsKey(XmlKeys.RAIL);

            if (mIsRail)
                if (mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                {
                    mHiBound = GridSpace.GetDrawingCoord(mOriginalPosition).X + (int.Parse(mOriginalInfo.mProperties[XmlKeys.LENGTH]) * 64);
                    mLowBound = GridSpace.GetDrawingCoord(mOriginalPosition).X;
                    mAxis = GridSpace.GetDrawingCoord(mOriginalPosition).Y;
                }
                else
                {
                    mHiBound = GridSpace.GetDrawingCoord(mOriginalPosition).Y + (int.Parse(mOriginalInfo.mProperties[XmlKeys.LENGTH]) * 64);
                    mLowBound = GridSpace.GetDrawingCoord(mOriginalPosition).Y;
                    mAxis = GridSpace.GetDrawingCoord(mOriginalPosition).X;
                }
            else
                mHiBound = mLowBound = mAxis = 0;

            UpdateBoundingBoxes();
            mMass = 1;
        }

        /// <summary>
        /// Respawns this object and stops its movement
        /// </summary>
        public override void Respawn()
        {
            base.Respawn();
            UpdateBoundingBoxes();
            mVelocity = Vector2.Zero;
        }

        /// <summary>
        /// Adds an additional force to the physics object
        /// </summary>
        /// <param name="force">Force to be added</param>
        public void AddForce(Vector2 force)
        {
            mAdditionalForces = Vector2.Add(mAdditionalForces, force);
        }

        /// <summary>
        /// Applies the force immediately. This will not be permanant
        /// </summary>
        /// <param name="force">Force to apply</param>
        public void ApplyImmediateForce(Vector2 force)
        {
            mVelocity = Vector2.Add(force, mVelocity);
        }

        /// <summary>
        /// TEMP METHOD - WILL GIVE THE PLAYER THE ABILITY TO FALL FROM ONE END OF THE SCREEN TO THE OTHER
        /// </summary>
        public void FixForBounds(int width, int height, bool isFixed)
        {
            if (!isFixed)
            {
                if (mPosition.X < 0) mPosition.X += width;
                if (mPosition.Y < 0) mPosition.Y += height;

                mPosition.X %= width;
                mPosition.Y %= height;
                return;
            }

            if (mPosition.X < 0) { mPosition.X = 0; mVelocity = new Vector2();}
            if (mPosition.Y < 0) {mPosition.Y = 0; mVelocity = new Vector2();}
            if (mPosition.X + this.mBoundingBox.Width > width) {mPosition.X = width - this.mBoundingBox.Width; mVelocity = new Vector2();}
            if (mPosition.Y + this.mBoundingBox.Height > height) { mPosition.Y = height - this.mBoundingBox.Height; mVelocity = new Vector2(); }
            
        }

        /// <summary>
        /// Reorient gravity in the given direction
        /// </summary>
        /// <param name="direction">Direction to enforce gravity on</param>
        public void ChangeGravityForceDirection(GravityDirections direction)
        {
            mResistiveForce = new Vector2(1, 1);

            if (direction == GravityDirections.Up || 
                direction == GravityDirections.Down) 
                mResistiveForce.X = mEnvironment.ErosionFactor;
            else 
                mResistiveForce.Y = mEnvironment.ErosionFactor;

            mGravityForce = mEnvironment.GravityForce;
        }

        /// <summary>
        /// Enforces a maximum speed that a force can 
        /// </summary>
        private void EnforceTerminalVelocity()
        {
            if (mVelocity.X > mEnvironment.TerminalSpeed)
                mVelocity.X = mEnvironment.TerminalSpeed;
            if (mVelocity.X < -mEnvironment.TerminalSpeed)
                mVelocity.X = -mEnvironment.TerminalSpeed;
            if (mVelocity.Y > mEnvironment.TerminalSpeed)
                mVelocity.Y = mEnvironment.TerminalSpeed;
            if (mVelocity.Y < -mEnvironment.TerminalSpeed)
                mVelocity.Y = -mEnvironment.TerminalSpeed;
        }

        /// <summary>
        /// Ensures a rail physics objects stays in its bounds
        /// </summary>
        private void EnforceRailBounds()
        {
            if (mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
            {

                if (mPosition.X > mHiBound)
                {
                    mPosition.X = mHiBound;
                    mVelocity = Vector2.Zero;
                }
                else if (mPosition.X < mLowBound)
                {
                    mPosition.X = mLowBound;
                    mVelocity = Vector2.Zero;
                }
                mPosition.Y = mAxis;
            }
            else
            {
                if (mPosition.Y > mHiBound)
                {
                    mPosition.Y = mHiBound;
                    mVelocity = Vector2.Zero;
                }
                else if (mPosition.Y < mLowBound)
                {
                    mPosition.Y = mLowBound;
                    mVelocity = Vector2.Zero;
                }
                mPosition.X = mAxis;
            }
        }

        /// <summary>
        /// Updates the bounding box around this object
        /// </summary>
        public void UpdateBoundingBoxes()
        {
            mBoundingBox = new Rectangle((int)mPosition.X, (int)mPosition.Y, (int)mSize.X, (int)mSize.Y);
        }

        /// <summary>
        /// Updates the velocity based on the force
        /// </summary>
        protected void UpdateVelocities()
        {
            if (mIsRail)
            {
                if(mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                    mVelocity.X += (mEnvironment.GravityForce.X / mMass) + mAdditionalForces.X;
                else if (mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_Y)
                    mVelocity.Y += (mEnvironment.GravityForce.Y / mMass) + mAdditionalForces.Y;
            }
            else
            {
                mVelocity = Vector2.Add(mVelocity, Vector2.Divide(mEnvironment.GravityForce, mMass));
                mVelocity = Vector2.Add(mVelocity, mAdditionalForces); 
            }

            //Force erosion on the resistive forces(friction/wind resistance)
            ChangeGravityForceDirection(mEnvironment.GravityDirection); 
            mVelocity = Vector2.Multiply(mVelocity, mResistiveForce);
            EnforceTerminalVelocity();

            if (mIsRail)
                EnforceRailBounds();
        }

        /// <summary>
        /// Update the physics object based on the given gametime
        /// </summary>
        /// <param name="gametime">Current gametime</param>
        public virtual void Update(GameTime gametime)
        {
            UpdateVelocities();
            mPrevPos = mPosition;
            mPosition = Vector2.Add(mPosition, mVelocity);
            UpdateBoundingBoxes();
        }

        #region Collision Code

        /// <summary>
        /// Returns true if the physics objects are colliding with each other
        /// (only good for 2 boxes)
        /// </summary>
        /// <param name="otherObject">The other object to test against</param>
        /// <returns>True if they are colliding with each other; False otherwise</returns>
        public virtual bool IsCollidingBoxAndBox(GameObject otherObject)
        {
            // if player has not collided with a hazard deeper than HAZARDFORGIVENESS pixels, do not handle the collision
            if (((this is Player) && (otherObject.CollisionType == XmlKeys.HAZARDOUS)) ||
                ((this.CollisionType == XmlKeys.HAZARDOUS) && (otherObject is Player)))
            {
                # region Find depth
                //Find Center
                float halfHeight1 = this.BoundingBox.Height / 2;
                float halfWidth1 = this.BoundingBox.Width / 2;

                //Calculate Center Position
                Vector2 center1 = new Vector2(this.BoundingBox.Left + halfWidth1, this.BoundingBox.Top + halfHeight1);

                //Find Center of otherObject
                float halfHeight2 = otherObject.BoundingBox.Height / 2;
                float halfWidth2 = otherObject.BoundingBox.Width / 2;

                //Calculate Center Position
                Vector2 center2 = new Vector2(otherObject.BoundingBox.Left + halfWidth2, otherObject.BoundingBox.Top + halfHeight2);

                //Center distances between both objects
                float distX = center1.X - center2.X;
                float distY = center1.Y - center2.Y;

                //Minimum distance 
                float minDistX = halfWidth1 + halfWidth2;
                float minDistY = halfHeight1 + halfHeight2;

                float depthX, depthY;
                if (distX > 0)
                {
                    depthX = minDistX - distX;
                }
                else
                {
                    depthX = -minDistX - distX;
                }
                if (distY > 0)
                {
                    depthY = minDistY - distY;
                }
                else
                {
                    depthY = -minDistY - distY;
                }
                depthX = Math.Abs(depthX);
                depthY = Math.Abs(depthY);
                #endregion 
                float shallow = Math.Min(depthX, depthY);
                if (shallow < HAZARDFORGIVENESS)
                    return false;
            }

            return !Equals(otherObject) && mBoundingBox.Intersects(otherObject.mBoundingBox);
        }

        /// <summary>
        /// Returns true if the physics objects are colliding with each other
        /// (only good for 2 boxes)
        /// </summary>
        /// <param name="otherObject">The other object to test against</param>
        /// <returns>True if they are colliding with each other; False otherwise</returns>
        public virtual bool IsCollidingBoxAndBoxAnimate(GameObject otherObject)
        {
            Rectangle animateArea = this.mBoundingBox;
            animateArea.Inflate(2, 2);
            return !Equals(otherObject) && animateArea.Intersects(otherObject.mBoundingBox);
        }

        /// <summary>
        /// Returns true if the physics objects are colliding with each other
        /// Circle = this
        /// Currently not used
        /// </summary>
        /// <param name="otherObject">The other object to test against</param>
        /// <returns>True if they are colliding with each other; False otherwise</returns>
        public virtual bool IsCollidingCircleAndBox(GameObject otherObject)
        {   
            BoundingSphere test1 = new BoundingSphere(
                new Vector3(BoundingBox.Center.X,BoundingBox.Center.Y,0f),(this.BoundingBox.Width/2)-1);

            BoundingSphere hazTest = new BoundingSphere(
                new Vector3(BoundingBox.Center.X,BoundingBox.Center.Y,0f),(this.BoundingBox.Width/2)-HAZARDFORGIVENESS);

            BoundingBox test2 = new BoundingBox(
                new Vector3(otherObject.BoundingBox.X, otherObject.BoundingBox.Y, 0f),
                new Vector3(otherObject.BoundingBox.X + otherObject.BoundingBox.Width, otherObject.BoundingBox.Y + otherObject.BoundingBox.Height, 0f));
            
            // if player has not collided with a hazard deeper than HAZARDFORGIVENESS pixels, do not handle the collision
            if (((this is Player) && (otherObject.CollisionType == XmlKeys.HAZARDOUS)) ||
                ((this.CollisionType == XmlKeys.HAZARDOUS) && (otherObject is Player)))
            {
                return hazTest.Intersects(test2);
            }

            return test1.Intersects(test2);
            
        }
        /// <summary>
        /// Returns true if the physics objects are colliding with each other
        /// square = this
        /// Currently not used
        /// </summary>
        /// <param name="otherObject">The other object to test against</param>
        /// <returns>True if they are colliding with each other; False otherwise</returns>
        public virtual bool IsCollidingBoxAndCircle(GameObject otherObject)
        {
            BoundingSphere test1 = new BoundingSphere(
                new Vector3(otherObject.BoundingBox.Center.X, otherObject.BoundingBox.Center.Y, 0f), (otherObject.BoundingBox.Width / 2)-1);

            BoundingSphere hazTest = new BoundingSphere(
                new Vector3(otherObject.BoundingBox.Center.X, otherObject.BoundingBox.Center.Y, 0f), (otherObject.BoundingBox.Width / 2)- HAZARDFORGIVENESS);

            BoundingBox test2 = new BoundingBox(
                new Vector3(BoundingBox.X, BoundingBox.Y, 0f),
                new Vector3(BoundingBox.X + BoundingBox.Width, BoundingBox.Y + BoundingBox.Height, 0f));

            // if player has not collided with a hazard deeper than HAZARDFORGIVENESS pixels, do not handle the collision
            if (((this is Player) && (otherObject.CollisionType == XmlKeys.HAZARDOUS)) ||
                ((this.CollisionType == XmlKeys.HAZARDOUS) && (otherObject is Player)))
            {
                return hazTest.Intersects(test2);
            }

            return test1.Intersects(test2);

        }
        /// <summary>
        /// Returns true if the physics objects are colliding with each other
        /// </summary>
        /// <param name="otherObject">The other object to test against</param>
        /// <returns>True if they are colliding with each other; False otherwise</returns>
        public virtual bool IsCollidingCircleandCircle(GameObject otherObject)
        {
            int radiusA = this.mBoundingBox.Width/2;
            int radiusB = otherObject.mBoundingBox.Width/2;
            Point centerPosA = this.mBoundingBox.Center;
            Point centerPosB = otherObject.mBoundingBox.Center;
            Vector2 centers = new Vector2((float)(centerPosA.X - centerPosB.X), (float)(centerPosA.Y - centerPosB.Y));

             // if player has not collided with a hazard deeper than HAZARDFORGIVENESS pixels, do not handle the collision
            if (((this is Player) && (otherObject.CollisionType == XmlKeys.HAZARDOUS)) ||
                ((this.CollisionType == XmlKeys.HAZARDOUS) && (otherObject is Player)))
            {
                if ((centers.Length() + HAZARDFORGIVENESS) > (radiusA+radiusB))
                    return false;
            }

            return !Equals(otherObject) && (centers.Length()<(radiusA+radiusB));
        }

        /// <summary>
        /// Decides on the collision detection method for this and the given object
        /// </summary>)
        /// <param name="obj">object we are testing</param>
        public bool HandleCollisions(GameObject obj)
        {
            if (!obj.IsSquare ^ !mIsSquare)
                return HandleCollideCircleAndBox(obj) == 1;
            else if (obj.IsSquare & mIsSquare)
                return HandleCollideBoxAndBox(obj) == 1;
            else
                return HandleCollideCircleAndCircle(obj) == 1;
        }
        /// <summary>
        /// Allows for only one collision to be done. 
        /// Whichever object this is colliding with deepest, handle only that collision. 
        /// </summary>
        /// <param name="objList">list of objects that this is colliding with</param>
        /// <returns></returns>
        public void HandleCollisionList(List<GameObject> objList)
        {
            if (objList.Count < 1)
                return;// short circuit

            if (objList.Count == 1)
            {
                HandleCollisions(objList.First());
                return;
            }
            
            float maxCollisionDepth = 0.0f;
            GameObject maxObject = null;

            List<StaticObject> wallList = new List<StaticObject>();

            foreach (GameObject gameObj in objList)
            {
                if (gameObj.CollisionType == XmlKeys.COLLECTABLE)
                    continue;

                if (gameObj is StaticObject)
                {
                    wallList.Add((StaticObject)gameObj);
                }

                if (gameObj is StaticObject)
                {
                    // 1st Priority
                    Vector2 depth = GetCollitionDepth(gameObj);
                    float shallowDepth = Math.Min(Math.Abs(depth.X), Math.Abs(depth.Y));
                    if ((shallowDepth >= maxCollisionDepth) ||
                        (maxObject == null) ||
                        (maxObject is PhysicsObject))
                    {
                        maxCollisionDepth = shallowDepth;// new deepest depth
                        maxObject = gameObj; // new top object
                    }

                }
                else if (gameObj is PhysicsObject)
                {
                    PhysicsObject physObj = (PhysicsObject)gameObj;
                    if (physObj.IsRail)
                    {
                        //2nd Priority
                        Vector2 depth = GetCollitionDepth(gameObj);
                        float shallowDepth = Math.Min(Math.Abs(depth.X), Math.Abs(depth.Y));
                        if (shallowDepth >= maxCollisionDepth)
                        {
                            if ((maxObject == null) ||
                                (!(maxObject is StaticObject)))
                            {
                                maxCollisionDepth = shallowDepth;// new deepest depth
                                maxObject = physObj; // new top object
                            }
                        }

                    }
                    else
                    {
                        //3rd priority
                        Vector2 depth = GetCollitionDepth(gameObj);
                        float shallowDepth = Math.Min(Math.Abs(depth.X), Math.Abs(depth.Y));
                        if (shallowDepth >= maxCollisionDepth)
                        {
                            if (maxObject == null)
                            {
                                maxCollisionDepth = shallowDepth;// new deepest depth
                                maxObject = physObj; // new top object
                            }
                            else if (maxObject is PhysicsObject)// Not Static
                            {
                                PhysicsObject maxPhys = (PhysicsObject)maxObject;
                                if (!maxPhys.IsRail)
                                {
                                    maxCollisionDepth = shallowDepth;// new deepest depth
                                    maxObject = physObj; // new top object
                                }
                            }
                        }

                    }
                }
            }//End foreach
            if (wallList.Count > 1)
            {
                HandleCollideBoxAndBox(maxObject);
                return;
            }
            if (maxObject != null)
            {
                HandleCollisions(maxObject);
            }
        }

        /// <summary>
        /// Handles collision for two boxes (this, and other)
        /// </summary>
        /// <param name="otherObject">object to do collision on(box)</param>
        /// <returns>1 if collided; 0 if no collision</returns>
        public virtual int HandleCollideBoxAndBox(GameObject otherObject)
        {
            if (!IsCollidingBoxAndBox(otherObject))
            {
                return 0;
            }
            
            //Player collided with collectable
            if (otherObject.CollisionType == XmlKeys.COLLECTABLE || this.CollisionType == XmlKeys.COLLECTABLE && !(otherObject is StaticObject))
                return 1;

            Vector2 colDepth = GetCollitionDepth(otherObject);

            // handle the shallowest collision
            if (Math.Abs(colDepth.X) > Math.Abs(colDepth.Y))// colliding top or bottom
            {

                //Reset Y Velocity to 0
                mVelocity.Y = 0;

                if (!mIsRail || (mIsRail && !(mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_Y)))
                    // reduce x velocity for friction
                    mVelocity.X *= otherObject.mFriction;

                // place the Y pos just so it is not colliding.
                mPosition.Y += colDepth.Y;
            }
            else// colliding left or right
            {

                //Reset X Velocity to 0
                mVelocity.X = 0;

                if (!mIsRail || (mIsRail && !(mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)))
                    // reduce Y velocity for friction
                    mVelocity.Y *= otherObject.mFriction;

                // place the X pos just so it is not colliding.
                mPosition.X += colDepth.X;
            }
            
            UpdateBoundingBoxes();
            return 1;// handled collision 
        }
        /// <summary>
        /// Handles collision for circle and circle
        /// </summary>
        /// <param name="otherObject">object to do collision on(circle)</param>
        /// <returns>1 if collided; 0 if no collision</returns>
        public virtual int HandleCollideCircleAndCircle(GameObject otherObject)
        {
            if (!IsCollidingCircleandCircle(otherObject))
            {
                return 0;
            }

            //Player collided with collectable
            if (otherObject.CollisionType == XmlKeys.COLLECTABLE || this.CollisionType == XmlKeys.COLLECTABLE && !(otherObject is StaticObject))
                return 1;
            
            Point centerA = this.mBoundingBox.Center;
            Point centerB = otherObject.BoundingBox.Center;

            Vector2 colDepth = GetCollitionDepth(otherObject);

            Vector2 centerDiff = new Vector2((float)(centerA.X - centerB.X), (float)(centerA.Y - centerB.Y));

            float radiusA = this.mBoundingBox.Width / 2;
            float radiusB = otherObject.mBoundingBox.Width / 2;

            float delta = (radiusA + radiusB) - centerDiff.Length();
            centerDiff.Normalize();
            Vector2 add = Vector2.Multiply(centerDiff, delta);

            HandleVelocitiesAfterCollision(otherObject, centerDiff);

            // place it just so it is not colliding. 
            if (otherObject is PhysicsObject)
            {
                if (!mIsRail)
                    mPosition += add / 2;
                else if (mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                    mPosition.X += add.X / 2;
                else
                    mPosition.Y += add.Y / 2;

                if(!((PhysicsObject)otherObject).IsRail)
                    ((PhysicsObject)otherObject).mPosition -= add / 2;
                else if (otherObject.OriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                    ((PhysicsObject)otherObject).mPosition.X -= add.X / 2;
                else
                    ((PhysicsObject)otherObject).mPosition.Y -= add.Y / 2;
            }
            else
            {
                // do not move a static object
                if(!mIsRail)
                    mPosition += add;
                else if (mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                    mPosition.X += add.X;
                else
                    mPosition.Y += add.Y;
            }

            UpdateBoundingBoxes();
            if (add.Length() > 1.0f)
            {
                return 1; // changed enough to call a collision
            }
            return 0;
        }
        /// <summary>
        /// Handles collision for circle and Box (circle =this)
        /// </summary>
        /// <param name="otherObject">object to do collision on(box)</param>
        /// <returns>1 if collided; 0 if no collision</returns>
        public virtual int HandleCollideCircleAndBox(GameObject otherObject)
        {
            if (!IsCollidingBoxAndBox(otherObject))
            {
                return 0;// no collision
            }
                
            //Player collided with collectable
            if (otherObject.CollisionType == XmlKeys.COLLECTABLE || this.CollisionType == XmlKeys.COLLECTABLE && !(otherObject is StaticObject))
                return 1;

            // get points of square
            Point[] p = new Point[4];
            // top left
            p[0] = new Point(otherObject.mBoundingBox.X,otherObject.mBoundingBox.Y);
            // top right
            p[1] = new Point(otherObject.mBoundingBox.X + otherObject.mBoundingBox.Width, p[0].Y);
            // bottom right
            p[2] = new Point(p[1].X, otherObject.mBoundingBox.Y + otherObject.mBoundingBox.Height);
            // bottom left
            p[3] = new Point(p[0].X, p[2].Y);

            Point center = this.BoundingBox.Center;
            // if not going to collide with a corner
            if (((center.X >= p[0].X) && (center.X <= p[1].X)) // top/bottom side
             || ((center.Y >= p[1].Y) && (center.Y <= p[2].Y)))// right/left side
            {
                // then treat like a square /square
                return HandleCollideBoxAndBox(otherObject);
            }
            else // going to hit a corner
            {
                // treat like circle/point collision
                Point centerA = this.mBoundingBox.Center;
                Point centerB = new Point();
                if ((center.X < p[0].X) && (center.Y < p[0].Y))// top left corner
                {
                    centerB = p[0];
                }
                else if ((center.X > p[1].X) && (center.Y < p[1].Y))// top right corner
                {
                    centerB = p[1];
                }
                else if ((center.X > p[2].X) && (center.Y > p[2].Y))// bottom right corner
                {
                    centerB = p[2];
                }
                else if ((center.X < p[3].X) && (center.Y > p[3].Y))// bottom left corner
                {
                    centerB = p[3];
                }

                Vector2 centerDiff = new Vector2((float)(centerA.X - centerB.X), (float)(centerA.Y - centerB.Y));

                float radiusA = this.mBoundingBox.Width / 2;

                float delta = (radiusA) - centerDiff.Length();
                centerDiff.Normalize();
                Vector2 add = Vector2.Multiply(centerDiff, delta);

                //Do not change velocity because we want an inelastic collision on corners.
                #region Corner magic
                // the best I could do for the constraints i am given.
                // Screw you people.
                // Love, 
                //  Curtis Taylor
                if ((Environment.GravityDirection == GravityDirections.Down) ||
                    (Environment.GravityDirection == GravityDirections.Up))
                {
                    mVelocity.X = 0f;
                    mVelocity.Y *= .96f;
                }
                else
                {
                    mVelocity.X *= .96f;
                    mVelocity.Y = 0f;
                }
                #endregion
                // place the Y pos just so it is not colliding.
                if (!mIsRail)
                    mPosition += add;
                else if (mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                    mPosition.X += add.X;
                else
                    mPosition.Y += add.Y;
                
                UpdateBoundingBoxes();
                if (add.Length() > 0.5f)
                {
                    return 1; // changed enough to call a collision
                }
            }
            return 0; // did not collide
        }

        /// <summary>
        /// finds how deep they are intersecting (That is what she said!)
        /// </summary>
        /// <returns>vector decribing depth</returns>
        public Vector2 GetCollitionDepth(GameObject otherObject)
        {
            //Find Center
            float halfHeight1 = this.BoundingBox.Height / 2;
            float halfWidth1 = this.BoundingBox.Width / 2;

            //Calculate Center Position
            Vector2 center1 = new Vector2(this.BoundingBox.Left + halfWidth1, this.BoundingBox.Top + halfHeight1);
            
            //Find Center of otherObject
            float halfHeight2 = otherObject.BoundingBox.Height / 2;
            float halfWidth2 = otherObject.BoundingBox.Width / 2;

            //Calculate Center Position
            Vector2 center2 = new Vector2(otherObject.BoundingBox.Left + halfWidth2, otherObject.BoundingBox.Top + halfHeight2);
            
            //Center distances between both objects
            float distX = center1.X - center2.X;
            float distY = center1.Y - center2.Y;

            //Minimum distance 
            float minDistX = halfWidth1 + halfWidth2;
            float minDistY = halfHeight1 + halfHeight2;

            if (!IsCollidingBoxAndBox(otherObject))
            {
                return Vector2.Zero;
            }

            float depthX, depthY;
            if (distX > 0)
            {
                depthX = minDistX - distX;
            }
            else
            {
                depthX = -minDistX - distX;
            }
            if (distY > 0)
            {
                depthY = minDistY - distY;
            }
            else
            {
                depthY = -minDistY - distY;
            }

            return new Vector2(depthX, depthY);
        }

        private void HandleVelocitiesAfterCollision(GameObject otherObject,Vector2 normal)
        {
            // Thanks to Dr. Bob of UofU SoC

            // normal of the collision
            Vector2 N = normal;
            N.Normalize();

            // tangent of the collision
            Vector2 T = new Vector2(N.Y, -N.X);

            float e = 0.9f;//0.9f; // elasticity of the collision

            float vain = Vector2.Dot(this.mVelocity, N);
            float vait = Vector2.Dot(this.mVelocity, T);

            float vbin, vbit;
            if (otherObject is PhysicsObject)
            {
                vbin = Vector2.Dot(((PhysicsObject)otherObject).mVelocity, N);
                vbit = Vector2.Dot(((PhysicsObject)otherObject).mVelocity, T);
            }
            else
            {
                vbin = Vector2.Dot(Vector2.Zero, N);
                vbit = Vector2.Dot(Vector2.Zero, T);
            }

            float vafn = ((e + 1.0f) * vbin + vain * (1 - e)) / 2;
            float vbfn = ((e + 1.0f) * vain - vbin * (1 - e)) / 2;
            float vaft = vait;
            float vbft = vbit;

            if (!mIsRail)
            {
                this.mVelocity.X = vafn * N.X + vaft * T.X;
                this.mVelocity.Y = vafn * N.Y + vaft * T.Y;
            }
            else if (mOriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                this.mVelocity.X = vafn * N.X + vaft * T.X;
            else
                this.mVelocity.Y = vafn * N.Y + vaft * T.Y;


            if (otherObject is PhysicsObject)
            {
                if (!((PhysicsObject)otherObject).IsRail)
                {
                    ((PhysicsObject)otherObject).mVelocity.X = vbfn * N.X + vbft * T.X;
                    ((PhysicsObject)otherObject).mVelocity.Y = vbfn * N.Y + vbft * T.Y;
                }
                else if (otherObject.OriginalInfo.mProperties[XmlKeys.RAIL] == XmlKeys.RAIL_X)
                    ((PhysicsObject)otherObject).mVelocity.X = vbfn * N.X + vbft * T.X;
                else
                    ((PhysicsObject)otherObject).mVelocity.Y = vbfn * N.Y + vbft * T.Y;
            }
        }

        #endregion

        public abstract int Kill();
        public abstract override string ToString();
    }
}

/*
            //// is colliding
            //if (otherObject is PhysicsObject)
            //{
            //    PhysicsObject PhysObj = (PhysicsObject)otherObject;

            //    // seperate balls (Tee hee!)
            //    // back the balls up along their previous path until they're not colliding

            //    // what percentage to increment moving back. .01 = 1% 
            //    float increment = .001f; //(Lower = more accurate; Higher = better performance
            //    float t = increment;
            //    while (IsCollidingCircleandCircle(PhysObj))
            //    {
            //        this.mPosition = this.mPrevPos;
            //        PhysObj.mPosition = PhysObj.mPrevPos;
            //        UpdateBoundingBoxes();
            //        t += increment;
            //    }

            //    // normal of the collision
            //    float n_x = this.mPosition.X - PhysObj.mPosition.X;
            //    float n_y = this.mPosition.Y - PhysObj.mPosition.Y;
            //    float n_length = (float)Math.Sqrt((n_x * n_x) + (n_y * n_y));
            //    // normalize n
            //    if (n_length > 0)
            //    {
            //        n_x /= n_length;
            //        n_y /= n_length;
            //    }

            //    // tangent of the collision
            //    float t_x = n_y;
            //    float t_y = -n_x;

            //    float e = 0.99f; // elasticity of the collision

            //    float vain = (this.mVelocity.X * n_x) + (this.mVelocity.Y * n_y); //Vector2.Dot(ball1.Velocity, N);
            //    float vait = (this.mVelocity.X * t_x) + (this.mVelocity.Y * t_y); //Vector2.Dot(ball1.Velocity, T);
            //    float vbin = (PhysObj.mVelocity.X * n_x) + (PhysObj.mVelocity.Y * n_y); //Vector2.Dot(ball2.Velocity, N);
            //    float vbit = (PhysObj.mVelocity.X * t_x) + (PhysObj.mVelocity.Y * t_y); //Vector2.Dot(ball2.Velocity, T);

            //    float vafn = ((e + 1.0f) * vbin + vain * (1 - e)) / 2;
            //    float vbfn = ((e + 1.0f) * vain - vbin * (1 - e)) / 2;
            //    float vaft = vait;
            //    float vbft = vbit;

            //    this.mVelocity.X = vafn * n_x + vaft * t_x;
            //    this.mVelocity.Y = vafn * n_y + vaft * t_y;
            //    PhysObj.mVelocity.X = vbfn * n_x + vbft * t_x;
            //    PhysObj.mVelocity.Y = vbfn * n_y + vbft * t_y;

            //}
            //else
            //{
            //    //just move "this"
            //    this.mVelocity = Vector2.Zero;
            //}
            */