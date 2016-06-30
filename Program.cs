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

            if (parent.ControlType == "Grid")
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

        public Dictionary<string, string> Attributes { get; set; }

        public Control()
        {
            Children = new List<Control>();
            Attributes = new Dictionary<string, string>();

            Key = Guid.NewGuid().ToString();
        }
    }

    class ContainerControl : Control
    {
        private const string rowAttributeName = "Grid.Row";
        private const string columnAttributeName = "Grid.Column";

        public void Insert(Control newControl, Control relativeTo, RelativePosition edge)
        {
            if (Children.Count > 0)
            {
                // Standard case, regular insert
                switch (edge)
                {
                    case RelativePosition.BottomOf:
                        var targetRow = GetRowOfChild(relativeTo) + 1;

                        newControl.Attributes[rowAttributeName] = (GetRowOfChild(relativeTo) + 1).ToString();
                        break;
                    case RelativePosition.RightOf:
                        newControl.Attributes[columnAttributeName] = (GetColumnOfChild(relativeTo) + 1).ToString();
                        break;
                    case RelativePosition.LeftOf:
                        // Put a to the right of b
                        if (GetColumnOfChild(relativeTo) < 1)
                        {
                            ShiftColumnsRight();
                        }
                        newControl.Attributes[columnAttributeName] = (GetColumnOfChild(relativeTo) - 1).ToString();
                        break;
                    case RelativePosition.TopOf:
                        // Put a below b
                        if (GetRowOfChild(relativeTo) < 1)
                        {
                            // Free up a slot by shifting rows
                            ShiftRowsDown();
                        }
                        newControl.Attributes[rowAttributeName] = (GetRowOfChild(relativeTo) - 1).ToString(); // Relative to new index.
                        break;
                    default:
                        throw new NotSupportedException($"I have no idea what {Enum.GetName(typeof(RelativePosition), edge)} is.");
                }

                Children.Add(newControl);
            }
            else
            {
                // Nothing in this yet, who cares?
                Children.Add(newControl);
            }

            // For debugging purposes only
            AssertIntegrity();
        }

        private void AssertIntegrity()
        {
            foreach (var childA in Children)
            {
                foreach (var childB in Children.Except(new[] { childA }))
                {
                    if (IsVerticalOrientation())
                    {
                        if (GetRowOfChild(childA) == GetRowOfChild(childB))
                        {
                            throw new Exception($"{childA.Key} and {childB.Key} somehow ended up with the same row!");
                        }
                    }
                    else
                    {
                        if (GetColumnOfChild(childA) == GetColumnOfChild(childB))
                        {
                            throw new Exception($"{childA.Key} and {childB.Key} somehow ended up with the same column!");
                        }
                    }
                }
            }

            // TODO: Check for gaps???
        }

        private Control GetAtRow(int row)
        {

        }

        private Control GetAtColumn(int column)
        {

        }

        private bool IsHorizontalPlacement(RelativePosition p)
        {
            return p == RelativePosition.LeftOf || p == RelativePosition.RightOf;
        }

        private bool IsVerticalPlacement(RelativePosition p)
        {
            return p == RelativePosition.TopOf || p == RelativePosition.BottomOf;
        }

        private int GetRowOfChild(Control immediateChild)
        {
            // First, check.
            if (Children.Count > 1 && IsHorizontalOrientation())
            {
                throw new ArgumentException($"Trying to get the row number of {immediateChild.Key} inside a horizontal container ({this.Key})!");
            }

            // Then, procure.
            if (immediateChild.Attributes.ContainsKey(rowAttributeName))
            {
                return int.Parse(immediateChild.Attributes[rowAttributeName]);
            }
            else
            {
                return 0;
            }
        }

        private int GetColumnOfChild(Control immediateChild)
        {
            // First, check.
            if (Children.Count > 1 && IsVerticalOrientation())
            {
                throw new ArgumentException($"Trying to get the column number of {immediateChild.Key} inside a vertical container ({this.Key})!");
            }

            // Then, procure.
            if (immediateChild.Attributes.ContainsKey(columnAttributeName))
            {
                return int.Parse(immediateChild.Attributes[columnAttributeName]);
            }
            else
            {
                return 0;
            }
        }

        private bool IsVerticalOrientation()
        {
            // Debugger method to tell me if the grid is considered to be 'vertical' or 'horizontal.'
            if (Children.Count < 2)
            {
                return true; // Sure do whatever
            }

            return (!Children.Any(x => x.Attributes.ContainsKey("Grid.Column")));
        }

        private bool IsHorizontalOrientation()
        {
            if (Children.Count < 2)
            {
                return true; // Actually indeterminate but whatever...
            }

            return (!Children.Any(x => x.Attributes.ContainsKey("Grid.Row")));
        }

        private void ShiftColumnsRight()
        {
            // Shifts all column entries right by one, so we can insert on the leftmost side
            const string key = "Grid.Column";
            foreach (var child in Children)
            {
                if (child.Attributes.ContainsKey(key))
                {
                    // Shift one right
                    child.Attributes[key] = (int.Parse(child.Attributes[key]) + 1).ToString();
                }
                else
                {
                    // Assume Grid.Column = 0 by default
                    child.Attributes[key] = "1";
                }
            }
        }

        private void ShiftRowsDown()
        {
            // Shifts all row entries down by one, so we can insert at the top
            const string key = "Grid.Row";
            foreach (var child in Children)
            {
                if (child.Attributes.ContainsKey(key))
                {
                    // Shift one right
                    child.Attributes[key] = (int.Parse(child.Attributes[key]) + 1).ToString();
                }
                else
                {
                    // Assume Grid.Column = 0 by default
                    child.Attributes[key] = "1";
                }
            }
        }
    }

    // TODO: Should I subclass Grid (ContainerControl?) so it has a bunch of convenience functions?

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
