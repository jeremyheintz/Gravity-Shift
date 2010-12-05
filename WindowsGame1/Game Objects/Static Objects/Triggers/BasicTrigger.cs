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

namespace GravityShift.Game_Objects.Static_Objects.Triggers
{
    class BasicTrigger : Trigger
    {
        List<PhysicsObject> affectedObjects = new List<PhysicsObject>();
        float mForce = .35f;
        Vector2 mDirection = new Vector2(1, 0);

        public BasicTrigger(ContentManager content, EntityInfo entity) :
            base(content, entity) 
        {
            if (entity.mProperties.ContainsKey(XmlKeys.FORCE))
                mForce = float.Parse(entity.mProperties[XmlKeys.FORCE]);
        }

        /// <summary>
        /// This triggers adds a force in the x direction while the player is within its bounds and removes it
        /// when the player exits
        /// </summary>
        /// <param name="objects">List of objects in the game</param>
        /// <param name="player">Player in the game</param>
        public override void RunTrigger(List<GameObject> objects, Player player)
        {
            foreach (GameObject gObj in objects)
            {
                if(gObj is PhysicsObject)
                {
                    //bool isColliding = (mIsSquare && player.IsCollidingBoxAndBox(this)) || (!IsSquare && player.IsCollidingCircleandCircle(this));
                    bool isColliding = mBoundingBox.Intersects(gObj.BoundingBox);
                    PhysicsObject pObj = (PhysicsObject)gObj;

                    if (!affectedObjects.Contains(pObj) && isColliding)
                    { pObj.AddForce(Vector2.Multiply(mDirection, mForce)); affectedObjects.Add(pObj); }
                    else if (affectedObjects.Contains(pObj) && !isColliding)
                    { pObj.AddForce(Vector2.Multiply(mDirection,-mForce)); affectedObjects.Remove(pObj); }                    
                }
            }
        }
    }
}