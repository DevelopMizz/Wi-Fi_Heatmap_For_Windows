using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WiFiServayTool.Models;

namespace WiFiServayTool;

public partial class Form1 : Form
{
    private readonly List<SignalMeasurement> _measurements = new();

    private readonly Button _loadButton;

    private readonly SurveyService _surveyService = new();

    private Image? _floorplan;

    public Form1()
    {
        InitializeComponent();

        DoubleBuffered = true;

        _loadButton = new Button
        {
            Text = "Load Floorplan",
            Dock = DockStyle.Top,
            Height = 40
        };

        _loadButton.Click += LoadButton_Click;

        Controls.Add(_loadButton);

        MouseClick += Form1_MouseClick;
    }

    private void LoadButton_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new();

        dialog.Filter =
            "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _floorplan = Image.FromFile(dialog.FileName);
            Invalidate();
        }
    }

    private void Form1_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        try
        {
            SignalMeasurement measurement =
                _surveyService.CaptureMeasurement(
                    e.X,
                    e.Y);

            _measurements.Add(measurement);

            Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Wi-Fi Measurement Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_floorplan is not null)
        {
            e.Graphics.DrawImage(
                _floorplan,
                new Rectangle(
                    0,
                    _loadButton.Bottom,
                    ClientSize.Width,
                    ClientSize.Height - _loadButton.Height));
        }

        foreach (SignalMeasurement measurement in _measurements)
        {
            using SolidBrush brush =
                new(GetSignalColour(measurement.SignalStrengthDbm));

            e.Graphics.FillEllipse(
                brush,
                (int)measurement.X - 20,
                (int)measurement.Y - 20,
                40,
                40);

            e.Graphics.DrawEllipse(
                Pens.Black,
                (int)measurement.X - 20,
                (int)measurement.Y - 20,
                40,
                40);

            e.Graphics.DrawString(
                $"{measurement.SignalStrengthDbm} dBm",
                Font,
                Brushes.Black,
                (int)measurement.X + 25,
                (int)measurement.Y - 10);
        }
    }

    private static Color GetSignalColour(int rssi)
    {
        if (rssi >= -55)
            return Color.Green;

        if (rssi >= -67)
            return Color.Yellow;

        if (rssi >= -75)
            return Color.Orange;

        return Color.Red;
    }
}