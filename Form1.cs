using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using WiFiServayTool.Models;

namespace WiFiServayTool;

public partial class Form1 : Form
{
    private readonly List<SignalMeasurement> _measurements = new();

    private readonly Button _loadButton;
    private readonly Button _saveButton;
    private readonly Panel _toolbarPanel;

    private readonly SurveyService _surveyService = new();

    private Image? _floorplan;

    public Form1()
    {
        InitializeComponent();

        DoubleBuffered = true;

        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40
        };

        _loadButton = new Button
        {
            Text = "Load Floorplan",
            Width = 120,
            Height = 30,
            Left = 5,
            Top = 5
        };

        _saveButton = new Button
        {
            Text = "Save Survey",
            Width = 120,
            Height = 30,
            Left = 130,
            Top = 5
        };

        _loadButton.Click += LoadButton_Click;
        _saveButton.Click += SaveButton_Click;

        _toolbarPanel.Controls.Add(_loadButton);
        _toolbarPanel.Controls.Add(_saveButton);

        Controls.Add(_toolbarPanel);

        MouseClick += Form1_MouseClick;
    }

    private void LoadButton_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new();

        dialog.Filter =
            "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _floorplan?.Dispose();

            using Image temp = Image.FromFile(dialog.FileName);
            _floorplan = new Bitmap(temp);

            _measurements.Clear();

            Invalidate();
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_floorplan is null)
        {
            MessageBox.Show(
                "No floorplan loaded.",
                "Save Survey",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        using SaveFileDialog dialog = new()
        {
            Filter = "PNG Image (*.png)|*.png",
            DefaultExt = "png",
            FileName = "WiFiSurvey.png"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            using Bitmap bitmap = new(
                ClientSize.Width,
                ClientSize.Height - _toolbarPanel.Height);

            using Graphics graphics =
                Graphics.FromImage(bitmap);

            graphics.Clear(Color.White);

            graphics.DrawImage(
                _floorplan,
                new Rectangle(
                    0,
                    0,
                    bitmap.Width,
                    bitmap.Height));

            foreach (SignalMeasurement measurement in _measurements)
            {
                using SolidBrush brush =
                    new(GetSignalColour(
                        measurement.SignalStrengthDbm));

                int x = (int)measurement.X;
                int y = (int)measurement.Y - _toolbarPanel.Height;

                graphics.FillEllipse(
                    brush,
                    x - 20,
                    y - 20,
                    40,
                    40);

                graphics.DrawEllipse(
                    Pens.Black,
                    x - 20,
                    y - 20,
                    40,
                    40);

                graphics.DrawString(
                    $"{measurement.SignalStrengthDbm} dBm",
                    Font,
                    Brushes.Black,
                    x + 25,
                    y - 10);
            }

            bitmap.Save(
                dialog.FileName,
                ImageFormat.Png);

            MessageBox.Show(
                "Survey saved successfully.",
                "Save Survey",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Save Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void Form1_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        if (_floorplan is null)
            return;

        if (e.Y < _toolbarPanel.Bottom)
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
                    _toolbarPanel.Bottom,
                    ClientSize.Width,
                    ClientSize.Height - _toolbarPanel.Bottom));
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