using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            root.Children.AddRange(new[] { a, b, c });

            var relationships = new List<PositioningRelationship>();

            // B is RightOf A
            relationships.Add(new PositioningRelationship { PositionedControl = b, RelativePositioning = RelativePosition.RightOf, DependsOn = a, });
            // C is RightOf A
            relationships.Add(new PositioningRelationship { PositionedControl = c, RelativePositioning = RelativePosition.RightOf, DependsOn = a });

            var p = new Program();
            p.Run(root, relationships);
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
            var grid = new ContainerControl { Key = "Grid" + a.Key + "-" + b.Key, ControlType = "Container" };

            grid.Children.Add(a); // Add it so we have something to base off of
            grid.Insert(b, a, positioning); // Insert relative to the new item

            return grid;
        }
    }

    class Control
    {
        public string Key { get; set; }
        public string ControlType { get; set; }
        public List<Control> Children { get; set; }

        public int GridColumn { get; set; }
        public int GridRow { get; set; }

        public Dictionary<string, string> Attributes { get; set; }

        public Control()
        {
            Children = new List<Control>();
            Attributes = new Dictionary<string, string>();

            GridRow = 0;
            GridColumn = 0;

            Key = Guid.NewGuid().ToString();
        }
    }

    class ContainerControl : Control
    {
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

        private Control GetAtRow(int row)
        {
            return Children.FirstOrDefault(c => c.GridRow == row);
        }

        private Control GetAtColumn(int column)
        {
            return Children.FirstOrDefault(c => c.GridColumn == column);
        }

        private bool IsHorizontalPlacement(RelativePosition p)
        {
            return p == RelativePosition.LeftOf || p == RelativePosition.RightOf;
        }

        private bool IsVerticalPlacement(RelativePosition p)
        {
            return p == RelativePosition.TopOf || p == RelativePosition.BottomOf;
        }
        
        private void ShiftColumnsRight()
        {
            // Shifts all column entries right by one, so we can insert on the leftmost side
            foreach (var child in Children)
            {
                child.GridColumn += 1;
            }
        }

        private void ShiftRowsDown()
        {
            // Shifts all row entries down by one, so we can insert at the top
            foreach (var child in Children)
            {
                child.GridRow += 1;
            }
        }
    }

    enum RelativePosition
    {
        RightOf,
        LeftOf,
        TopOf,
        BottomOf
    }

    class PositioningRelationship
    {
        public Control DependsOn { get; set; }
        public Control PositionedControl { get; set; }
        public RelativePosition RelativePositioning { get; set; }
    }
}
