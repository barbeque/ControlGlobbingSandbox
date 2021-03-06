﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ControlGlobbingSandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            var root = new Control { Key = "Root", ControlType = "Form" };

            var a = new Control { Key = "A", ControlType = "Data" };
            var b = new Control { Key = "B", ControlType = "Data" };
            var c = new Control { Key = "C", ControlType = "Data" };
            var d = new Control { Key = "D", ControlType = "Data" };

            root.Children.AddRange(new[] { a, b, c, d });

            var relationships = new List<PositioningRelationship>();

            // B is RightOf A - tests creating a new grid to position two normal elements
            relationships.Add(new PositioningRelationship { PositionedControl = b, RelativePositioning = RelativePosition.RightOf, DependsOn = a, });
            // C is RightOf A - tests hitting an occupied cell, and creating a new grid as a result
            relationships.Add(new PositioningRelationship { PositionedControl = c, RelativePositioning = RelativePosition.RightOf, DependsOn = a });
            // D is RightOf A - tests hitting an occupied cell, which is also the grid containing <B, C>, and having to add itself to that grid.
            relationships.Add(new PositioningRelationship { PositionedControl = d, RelativePositioning = RelativePosition.RightOf, DependsOn = a });

            var p = new Program();
            p.Run(root, relationships);

            // Serialize to XElement
            var xmlSerializer = new XmlSerializer(typeof(Control));
            var xmlString = string.Empty;

            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms))
                {
                    xmlSerializer.Serialize(writer, root);
                    xmlString = Encoding.ASCII.GetString(ms.ToArray());
                }
            }

            Console.WriteLine(xmlString);
        }

        public void Run(Control root, List<PositioningRelationship> constraints)
        {
            // OK use the relationships to transform.
            foreach (var relationship in constraints)
            {
                ApplyRelationship(root, relationship);
            }
        }

        private Control GetParentOf(Control targetChild, Control root)
        {
            if (root == targetChild)
            {
                return null; // root has no parent
            }

            if (root.Children.Contains(targetChild))
            {
                return root;
            }
            else
            {
                foreach (var myKid in root.Children)
                {
                    var result = GetParentOf(targetChild, myKid);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private void ApplyRelationship(Control root, PositioningRelationship relationship)
        {
            // basic operation, just clean these out
            var parent = GetParentOf(relationship.DependsOn, root);

            if (parent.ControlType == "Container")
            {
                // We are transferring from one container to the other, so get the old parent of the control we are globbing
                var positionedControlParent = GetParentOf(relationship.PositionedControl, root);

                AttachToExistingGrid(parent, relationship.DependsOn, relationship.PositionedControl, relationship.RelativePositioning);

                // Remove the positioned control from its parent
                positionedControlParent.Children.Remove(relationship.PositionedControl);

                // The grid is already in the tree, so nothing else has to change.
            }
            else
            {
                // Create new grid
                var gridContainer = WrapInGrid(relationship.DependsOn, relationship.PositionedControl, relationship.RelativePositioning);

                // Remove the kids from immediate children list
                parent.Children.Remove(relationship.DependsOn);
                parent.Children.Remove(relationship.PositionedControl);

                // Put in the grid, which should contain the kids.
                parent.Children.Add(gridContainer);
            }
        }

        private Control AttachToExistingGrid(Control grid, Control target, Control addingMe, RelativePosition positioning)
        {
            // OK we know that we have a grid containing the target control.
            (grid as ContainerControl).Insert(addingMe, target, positioning);
            return grid;
        }

        private Control WrapInGrid(Control a, Control b, RelativePosition positioning)
        {
            // Case 1: Create new grid
            var grid = new ContainerControl { Key = "Grid" + a.Key + "-" + b.Key };

            grid.Children.Add(a); // Add it so we have something to base off of
            grid.Insert(b, a, positioning); // Insert relative to the new item

            return grid;
        }
    }

    /// <summary>
    /// Any control that can have children. Should render to specific ControlTypes.
    /// </summary>
    [XmlInclude(typeof(ContainerControl))]
    [Serializable] public class Control
    {
        [XmlElement("ID")]
        public string Key { get; set; }

        [XmlElement("ControlType")]
        public string ControlType { get; set; }

        [XmlArray("ContainedControls")]
        public List<Control> Children { get; set; }

        [XmlElement("Grid.Column")]
        public int GridColumn { get; set; }

        [XmlElement("Grid.Row")]
        public int GridRow { get; set; }
        
        public Control()
        {
            Children = new List<Control>();

            GridRow = 0;
            GridColumn = 0;

            Key = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// A control designed to control positioning of one or more controls. Should render to the WPF Grid type.
    /// </summary>
    public class ContainerControl : Control
    {
        /// <summary>
        /// Insert a control into this grid, relative to another control already present in the grid.
        /// </summary>
        /// <param name="newControl">The newly added control.</param>
        /// <param name="relativeTo">A control already in the grid that you must position relative to.</param>
        /// <param name="edge">Which edge of <see cref="relativeTo"/> to position against. If there are no controls in the grid, this directive will be ignored.</param>
        public void Insert(Control newControl, Control relativeTo, RelativePosition edge)
        {
            if (Children.Count > 0)
            {
                if(IsVerticalPlacement(edge))
                {
                    // Vertical insert
                    int targetRow = relativeTo.GridRow + (edge == RelativePosition.TopOf ? -1 : 1);

                    if(targetRow < 0)
                    {
                        ShiftRowsDown();
                    }

                    var alreadyThere = GetAtRow(targetRow);
                    
                    if(alreadyThere != null)
                    {
                        if (alreadyThere.ControlType == "Container")
                        {
                            // The element is already a grid so we've done all we can, just glob it into the original grid at what we assume is the correct thing.
                            (alreadyThere as ContainerControl).GlobIntoGrid(newControl);
                        }
                        else
                        {
                            // Uhhhh I assume we're going to create a grid where C is right of B.
                            var newGrid = new ContainerControl();

                            // Reset old row settings, if any
                            alreadyThere.GridRow = 0;
                            newGrid.Children.Add(alreadyThere); // add to new grid
                            this.Children.Remove(alreadyThere); // remove from me

                            // now insert the new guy into the new grid, i guess now creating a dependency
                            newGrid.Insert(newControl, alreadyThere, RelativePosition.RightOf);

                            // now insert the grid, replacing the element that was there before.
                            // this should work?
                            this.Insert(newGrid, relativeTo, edge);
                        }
                    }
                    else
                    {
                        newControl.GridRow = targetRow;
                        Children.Add(newControl);
                    }
                }
                else
                {
                    // Horizontal insert
                    int targetColumn = relativeTo.GridColumn + (edge == RelativePosition.LeftOf ? -1 : 1);

                    if(targetColumn < 0)
                    {
                        ShiftColumnsRight();
                    }

                    var alreadyThere = GetAtColumn(targetColumn);

                    if(alreadyThere != null)
                    {
                        if(alreadyThere.ControlType == "Container")
                        {
                            // The element is already a grid so we've done all we can, just glob it into the original grid at what we assume is the correct thing.
                            (alreadyThere as ContainerControl).GlobIntoGrid(newControl);
                        }
                        else
                        {
                            // Uhhhh I assume we're going to create a grid where C is below B.
                            var newGrid = new ContainerControl();

                            // Reset old column settings if any
                            alreadyThere.GridColumn = 0;
                            newGrid.Children.Add(alreadyThere); // add to new grid
                            this.Children.Remove(alreadyThere); // remove from me

                            // now insert the new guy into the new grid, i guess now creating a dependency
                            newGrid.Insert(newControl, alreadyThere, RelativePosition.BottomOf);

                            // now insert the grid, replacing the element that was there before.
                            // this should work?
                            this.Insert(newGrid, relativeTo, edge);
                        }
                    }
                    else
                    {
                        newControl.GridColumn = targetColumn;
                        Children.Add(newControl);
                    }
                }
            }
            else
            {
                // Nothing in this yet, who cares?
                newControl.GridRow = newControl.GridColumn = 0;
                Children.Add(newControl);
            }
        }      

        /// <summary>
        /// Call this when you are just adding a control to some grid,
        /// where it doesn't really matter what you're doing.
        /// </summary>
        /// <param name="putAtTheEnd">The control being inserted.</param>
        private void GlobIntoGrid(Control putAtTheEnd)
        {
            // Figure out what the 'orientation' of the grid is
            var totalColumns = Children.Sum(x => x.GridColumn);
            var totalRows = Children.Sum(x => x.GridRow);

            if(totalColumns > totalRows)
            {
                // Horizontal
                putAtTheEnd.GridColumn = Children.Max(x => x.GridColumn) + 1;
                putAtTheEnd.GridRow = 0;
            }
            else
            {
                // Vertical
                putAtTheEnd.GridColumn = 0;
                putAtTheEnd.GridRow = Children.Max(x => x.GridRow) + 1;
            }
            
            Children.Add(putAtTheEnd);
        }

        /// <summary>
        /// Get the control living at a given row in this container.
        /// 
        /// Assumes the container is strictly vertical, such that row indices resolve to a unique control.
        /// </summary>
        /// <param name="row">The row #.</param>
        /// <returns>The control at this row.</returns>
        private Control GetAtRow(int row)
        {
            return Children.FirstOrDefault(c => c.GridRow == row);
        }

        /// <summary>
        /// Get the control living at a given column in this container.
        /// 
        /// Assumes the container is strictly horizontal, such that columnar indices resolve to a unique control.
        /// </summary>
        /// <param name="column">The column #.</param>
        /// <returns>The control at this column.</returns>
        private Control GetAtColumn(int column)
        {
            return Children.FirstOrDefault(c => c.GridColumn == column);
        }

        /// <summary>
        /// Tells us if a requested relative position strategy is 'horizontal' or not.
        /// 
        /// Useful primarily for breaking up code along column/row processing rules.
        /// </summary>
        /// <param name="p">The placement rule</param>
        /// <returns><c>true</c> if the placement strategy relates to left/right</returns>
        private bool IsHorizontalPlacement(RelativePosition p)
        {
            return p == RelativePosition.LeftOf || p == RelativePosition.RightOf;
        }

        /// <summary>
        /// Tells us if a requested relative position strategy is 'vertical' or not.
        /// 
        /// Useful primarily for breaking up code along column/row processing rules.
        /// </summary>
        /// <param name="p">The placement rule</param>
        /// <returns><c>true</c> if the placement strategy relates to above/below</returns>
        private bool IsVerticalPlacement(RelativePosition p)
        {
            return p == RelativePosition.TopOf || p == RelativePosition.BottomOf;
        }
        
        /// <summary>
        /// For all items in the grid, add 1 to their column index.
        /// 
        /// Intended to be used so we can insert on the leftmost cell.
        /// </summary>
        private void ShiftColumnsRight()
        {
            foreach (var child in Children)
            {
                child.GridColumn += 1;
            }
        }

        /// <summary>
        /// For all items in the grid, add 1 to their row index.
        /// 
        /// Intended to be used so we can insert on the topmost cell.
        /// </summary>
        private void ShiftRowsDown()
        {
            // Shifts all row entries down by one, so we can insert at the top
            foreach (var child in Children)
            {
                child.GridRow += 1;
            }
        }
        
        public ContainerControl()
        {
            ControlType = "Container";
        }
    }

    /// <summary>
    /// Which edge of a control is used for relative positioning.
    /// </summary>
    public enum RelativePosition
    {
        RightOf,
        LeftOf,
        TopOf,
        BottomOf
    }

    /// <summary>
    /// The constraints of positioning two elements relative to each other.
    /// </summary>
    class PositioningRelationship
    {
        /// <summary>
        /// The control that we are positioning relative to.
        /// </summary>
        public Control DependsOn { get; set; }
        
        /// <summary>
        /// The control being positioned relative to <see cref="DependsOn"/>.
        /// </summary>
        public Control PositionedControl { get; set; }

        /// <summary>
        /// How the control will be positioned (on which edge of <see cref="DependsOn"/>.)
        /// </summary>
        public RelativePosition RelativePositioning { get; set; }
    }
}
