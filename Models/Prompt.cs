namespace WiFiServayTool.Models;

public static class Prompt
{
    public static string? Show(
        string text,
        string caption)
    {
        Form form = new()
        {
            Width = 300,
            Height = 150,
            Text = caption,
            StartPosition = FormStartPosition.CenterParent
        };

        Label label = new()
        {
            Left = 10,
            Top = 10,
            Width = 260,
            Text = text
        };

        TextBox textBox = new()
        {
            Left = 10,
            Top = 35,
            Width = 260
        };

        Button button = new()
        {
            Text = "OK",
            Left = 195,
            Width = 75,
            Top = 70,
            DialogResult = DialogResult.OK
        };

        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(button);

        form.AcceptButton = button;

        return form.ShowDialog() == DialogResult.OK
            ? textBox.Text
            : null;
    }
}