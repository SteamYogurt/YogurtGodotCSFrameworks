using Godot;

public static class UIUtils
{
    public static Button FindFirstFocusButton(Control root)
    {
        if (root == null)
            return null;

        if (root is Button button && button.Visible && !button.Disabled)
            return button;

        foreach (Node child in root.GetChildren())
        {
            if (child is Control control)
            {
                var result = FindFirstFocusButton(control);
                if (result != null)
                    return result;
            }
        }

        return null;
    }
}
