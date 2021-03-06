﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections;
using System.Windows.Forms;
using GravityLevelEditor.EntityCreationForm;

namespace GravityLevelEditor.GuiTools
{
    class SingleSelect:ITool
    {
        Point mInitial;
        Point mPrevious;

        bool mouseDown = false;

        #region ITool Members

        public void LeftMouseDown(ref EditorData data, Point gridPosition)
        {
            mPrevious = mInitial = gridPosition;
            mouseDown = true;
        }
        
        public void LeftMouseUp(ref EditorData data, Point gridPosition)
        {
            if(gridPosition.Equals(mInitial))
            {
                if(!data.CTRLHeld) data.SelectedEntities.Clear();
                Entity selected = data.Level.SelectEntity(gridPosition);
                if (selected != null && !data.SelectedEntities.Contains(selected))
                    data.SelectedEntities.Add(selected);
                else if (selected == null && !data.CTRLHeld)
                    data.SelectedEntities.Clear();
            }
            if (data.SelectedEntities.Count > 0)
            {
                data.Level.MoveEntity(data.SelectedEntities,
                    new Size(Point.Subtract(mInitial, new Size(gridPosition))), false);
                data.Level.MoveEntity(data.SelectedEntities,
                    new Size(Point.Subtract(gridPosition, new Size(mInitial))), true);
            }
            mouseDown = false;
        }


        public void RightMouseDown(ref EditorData data, Point gridPosition)
        {
            if(data.SelectedEntities.Count != 1) return;

            AdditionalProperties properties = new AdditionalProperties(data.Level.SelectEntity(gridPosition).Properties);
            properties.Editable = false;
            if (properties.ShowDialog() == DialogResult.OK)
                data.Level.SelectEntity(gridPosition).Properties = properties.Properties;
        }

        public void RightMouseUp(ref EditorData data, Point gridPosition)
        {
            
        }

        public void MouseMove(ref EditorData data, Panel panel, Point gridPosition)
        {
            if (!mPrevious.Equals(gridPosition) && data.SelectedEntities.Count > 0 && mouseDown)
            {
                //Keep an eye on this. The SelectedEntities can return an empty list
                data.Level.MoveEntity(data.SelectedEntities,
                    new Size(Point.Subtract(gridPosition, new Size(mPrevious))), false);
                mPrevious = gridPosition;
                panel.Invalidate(panel.DisplayRectangle);
            }
        }

        #endregion
    }
}
