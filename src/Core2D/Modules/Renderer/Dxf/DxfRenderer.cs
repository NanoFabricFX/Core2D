﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Core2D.Model;
using Core2D.Model.Renderer;
using Core2D.Model.Style;
using Core2D.ViewModels;
using Core2D.ViewModels.Path;
using Core2D.ViewModels.Path.Segments;
using Core2D.ViewModels.Renderer;
using Core2D.ViewModels.Shapes;
using Core2D.ViewModels.Style;
using DXF = netDxf;
using DXFE = netDxf.Entities;
using DXFO = netDxf.Objects;
using DXFT = netDxf.Tables;

namespace Core2D.Modules.Renderer.Dxf;

public partial class DxfRenderer : ViewModelBase, IShapeRenderer
{
    private readonly ICache<string, DXFO.ImageDefinition>? _biCache;
    private readonly double _sourceDpi = 96.0;
    private readonly double _targetDpi = 72.0;
    private double _pageWidth;
    private double _pageHeight;
    private string? _outputPath;
    internal DXFT.Layer? _currentLayer;

    [AutoNotify] private ShapeRendererStateViewModel? _state;

    public DxfRenderer(IServiceProvider? serviceProvider) : base(serviceProvider)
    {
        _state = serviceProvider.GetService<IViewModelFactory>()?.CreateShapeRendererState();
        _biCache = serviceProvider.GetService<IViewModelFactory>()?.CreateCache<string, DXFO.ImageDefinition>();
    }

    public override object Copy(IDictionary<object, object>? shared)
    {
        throw new NotImplementedException();
    }

    private static readonly double s_lineweightFactor = 96.0 / 2540.0;

    private static readonly short[] s_lineweights = { -3, -2, -1, 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 };

    private static DXF.Lineweight ToLineweight(double thickness)
    {
        short lineweight = (short)(thickness / s_lineweightFactor);
        return (DXF.Lineweight)s_lineweights.OrderBy(x => Math.Abs((long)x - lineweight)).First();
    }

    private static DXF.AciColor ToColor(BaseColorViewModel colorViewModel)
    {
        return colorViewModel switch
        {
            ArgbColorViewModel argbColor => new DXF.AciColor(argbColor.R, argbColor.G, argbColor.B),
            _ => throw new NotSupportedException($"The {colorViewModel.GetType()} color type is not supported."),
        };
    }

    private static short ToTransparency(BaseColorViewModel colorViewModel)
    {
        switch (colorViewModel)
        {
            case ArgbColorViewModel argbColor:
                return (short)(90.0 - argbColor.A * 90.0 / 255.0);
                ;
            default:
                throw new NotSupportedException($"The {colorViewModel.GetType()} color type is not supported.");
        }
    }

    private double ToDxfX(double x) => x;

    private double ToDxfY(double y) => _pageHeight - y;

    private DXFE.Line CreateLine(double x1, double y1, double x2, double y2)
    {
        double _x1 = ToDxfX(x1);
        double _y1 = ToDxfY(y1);
        double _x2 = ToDxfX(x2);
        double _y2 = ToDxfY(y2);
        return new DXFE.Line(new DXF.Vector3(_x1, _y1, 0), new DXF.Vector3(_x2, _y2, 0));
    }

    private DXFE.Ellipse CreateEllipse(double x, double y, double width, double height)
    {
        double _cx = ToDxfX(x + width / 2.0);
        double _cy = ToDxfY(y + height / 2.0);
        double minor = Math.Min(height, width);
        double major = Math.Max(height, width);

        return new DXFE.Ellipse()
        {
            Center = new DXF.Vector3(_cx, _cy, 0),
            MajorAxis = major,
            MinorAxis = minor,
            StartAngle = 0.0,
            EndAngle = 360.0,
            Rotation = height > width ? 90.0 : 0.0
        };
    }

    private DXFE.Ellipse CreateEllipticalArc(ArcShapeViewModel arc)
    {
        var a = new Spatial.Arc.GdiArc(
            Spatial.Point2.FromXY(arc.Point1.X, arc.Point1.Y),
            Spatial.Point2.FromXY(arc.Point2.X, arc.Point2.Y),
            Spatial.Point2.FromXY(arc.Point3.X, arc.Point3.Y),
            Spatial.Point2.FromXY(arc.Point4.X, arc.Point4.Y));

        double _cx = ToDxfX(a.X + a.Width / 2.0);
        double _cy = ToDxfY(a.Y + a.Height / 2.0);
        double minor = Math.Min(a.Height, a.Width);
        double major = Math.Max(a.Height, a.Width);
        double startAngle = -a.EndAngle;
        double endAngle = -a.StartAngle;
        double rotation = 0;

        if (a.Height > a.Width)
        {
            startAngle += 90;
            endAngle += 90;
            rotation = -90;
        }

        return new DXFE.Ellipse()
        {
            Center = new DXF.Vector3(_cx, _cy, 0),
            MajorAxis = major,
            MinorAxis = minor,
            StartAngle = startAngle,
            EndAngle = endAngle,
            Rotation = rotation
        };
    }

    private DXFE.Spline CreateQuadraticSpline(double p1x, double p1y, double p2x, double p2y, double p3x, double p3y)
    {
        double _p1x = ToDxfX(p1x);
        double _p1y = ToDxfY(p1y);
        double _p2x = ToDxfX(p2x);
        double _p2y = ToDxfY(p2y);
        double _p3x = ToDxfX(p3x);
        double _p3y = ToDxfY(p3y);

        return new DXFE.Spline(
            new List<DXFE.SplineVertex>
            {
                new DXFE.SplineVertex(_p1x, _p1y, 0.0),
                new DXFE.SplineVertex(_p2x, _p2y, 0.0),
                new DXFE.SplineVertex(_p3x, _p3y, 0.0)
            }, 2);
    }

    private DXFE.Spline CreateCubicSpline(double p1x, double p1y, double p2x, double p2y, double p3x, double p3y, double p4x, double p4y)
    {
        double _p1x = ToDxfX(p1x);
        double _p1y = ToDxfY(p1y);
        double _p2x = ToDxfX(p2x);
        double _p2y = ToDxfY(p2y);
        double _p3x = ToDxfX(p3x);
        double _p3y = ToDxfY(p3y);
        double _p4x = ToDxfX(p4x);
        double _p4y = ToDxfY(p4y);

        return new DXFE.Spline(
            new List<DXFE.SplineVertex>
            {
                new DXFE.SplineVertex(_p1x, _p1y, 0.0),
                new DXFE.SplineVertex(_p2x, _p2y, 0.0),
                new DXFE.SplineVertex(_p3x, _p3y, 0.0),
                new DXFE.SplineVertex(_p4x, _p4y, 0.0)
            }, 3);
    }

    private void DrawLineInternal(DXF.DxfDocument dxf, DXFT.Layer layer, ShapeStyleViewModel style, bool isStroked, double x1, double y1, double x2, double y2)
    {
        if (isStroked)
        {
            var stroke = ToColor(style.Stroke.Color);
            var strokeTansparency = ToTransparency(style.Stroke.Color);
            var lineweight = ToLineweight(style.Stroke.Thickness);

            var dxfLine = CreateLine(x1, y1, x2, y2);

            dxfLine.Layer = layer;
            dxfLine.Color = stroke;
            dxfLine.Transparency.Value = strokeTansparency;
            dxfLine.Lineweight = lineweight;

            dxf.AddEntity(dxfLine);
        }
    }

    private void DrawLineInternal(DXF.DxfDocument dxf, DXFT.Layer layer, BaseColorViewModel colorViewModel, double thickness, double x1, double y1, double x2, double y2)
    {
        var stroke = ToColor(colorViewModel);
        var strokeTansparency = ToTransparency(colorViewModel);
        var lineweight = ToLineweight(thickness);

        var dxfLine = CreateLine(x1, y1, x2, y2);

        dxfLine.Layer = layer;
        dxfLine.Color = stroke;
        dxfLine.Transparency.Value = strokeTansparency;
        dxfLine.Lineweight = lineweight;

        dxf.AddEntity(dxfLine);
    }

    private void DrawRectangleInternal(DXF.DxfDocument dxf, DXFT.Layer layer, bool isFilled, bool isStroked, ShapeStyleViewModel style, ref Spatial.Rect2 rect)
    {
        if (isFilled)
        {
            FillRectangle(dxf, layer, rect.X, rect.Y, rect.Width, rect.Height, style.Fill.Color);
        }

        if (isStroked)
        {
            StrokeRectangle(dxf, layer, style, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    private void FillRectangle(DXF.DxfDocument dxf, DXFT.Layer layer, double x, double y, double width, double height, BaseColorViewModel colorViewModel)
    {
        var fill = ToColor(colorViewModel);
        var fillTransparency = ToTransparency(colorViewModel);

        var bounds =
            new List<DXFE.HatchBoundaryPath>
            {
                new DXFE.HatchBoundaryPath(
                    new List<DXFE.EntityObject>
                    {
                        CreateLine(x, y, x + width, y),
                        CreateLine(x + width, y, x + width, y + height),
                        CreateLine(x + width, y + height, x, y + height),
                        CreateLine(x, y + height, x, y)
                    })
            };

        var hatch = new DXFE.Hatch(DXFE.HatchPattern.Solid, bounds, false)
        {
            Layer = layer,
            Color = fill
        };
        hatch.Transparency.Value = fillTransparency;

        dxf.AddEntity(hatch);
    }

    private void StrokeRectangle(DXF.DxfDocument dxf, DXFT.Layer layer, ShapeStyleViewModel style, double x, double y, double width, double height)
    {
        DrawLineInternal(dxf, layer, style, true, x, y, x + width, y);
        DrawLineInternal(dxf, layer, style, true, x + width, y, x + width, y + height);
        DrawLineInternal(dxf, layer, style, true, x + width, y + height, x, y + height);
        DrawLineInternal(dxf, layer, style, true, x, y + height, x, y);
    }

    private void StrokeRectangle(DXF.DxfDocument dxf, DXFT.Layer layer, BaseColorViewModel colorViewModel, double thickness, double x, double y, double width, double height)
    {
        DrawLineInternal(dxf, layer, colorViewModel, thickness, x, y, x + width, y);
        DrawLineInternal(dxf, layer, colorViewModel, thickness, x + width, y, x + width, y + height);
        DrawLineInternal(dxf, layer, colorViewModel, thickness, x + width, y + height, x, y + height);
        DrawLineInternal(dxf, layer, colorViewModel, thickness, x, y + height, x, y);
    }

    private void DrawEllipseInternal(DXF.DxfDocument dxf, DXFT.Layer layer, bool isFilled, bool isStroked, ShapeStyleViewModel style, ref Spatial.Rect2 rect)
    {
        var dxfEllipse = CreateEllipse(rect.X, rect.Y, rect.Width, rect.Height);

        if (isFilled)
        {
            FillEllipse(dxf, layer, dxfEllipse, style.Fill.Color);
        }

        if (isStroked)
        {
            StrokeEllipse(dxf, layer, dxfEllipse, style.Stroke.Color, style.Stroke.Thickness);
        }
    }

    private void StrokeEllipse(DXF.DxfDocument dxf, DXFT.Layer layer, DXFE.Ellipse dxfEllipse, BaseColorViewModel colorViewModel, double thickness)
    {
        var stroke = ToColor(colorViewModel);
        var strokeTansparency = ToTransparency(colorViewModel);
        var lineweight = ToLineweight(thickness);

        dxfEllipse.Layer = layer;
        dxfEllipse.Color = stroke;
        dxfEllipse.Transparency.Value = strokeTansparency;
        dxfEllipse.Lineweight = lineweight;

        dxf.AddEntity(dxfEllipse);
    }

    private void FillEllipse(DXF.DxfDocument dxf, DXFT.Layer layer, DXFE.Ellipse dxfEllipse, BaseColorViewModel colorViewModel)
    {
        var fill = ToColor(colorViewModel);
        var fillTransparency = ToTransparency(colorViewModel);

        // TODO: The netDxf does not create hatch for Ellipse with end angle equal to 360.
        var bounds =
            new List<DXFE.HatchBoundaryPath>
            {
                new DXFE.HatchBoundaryPath(
                    new List<DXFE.EntityObject>
                    {
                        (DXFE.Ellipse)dxfEllipse.Clone()
                    })
            };

        var hatch = new DXFE.Hatch(DXFE.HatchPattern.Solid, bounds, false)
        {
            Layer = layer,
            Color = fill
        };
        hatch.Transparency.Value = fillTransparency;

        dxf.AddEntity(hatch);
    }

    private void DrawGridInternal(DXF.DxfDocument dxf, DXFT.Layer layer, IGrid grid, ref Spatial.Rect2 rect)
    {
        double ox = rect.X;
        double ex = rect.X + rect.Width;
        double oy = rect.Y;
        double ey = rect.Y + rect.Height;
        double cw = grid.GridCellWidth;
        double ch = grid.GridCellHeight;

        for (double gx = ox + cw; gx < ex; gx += cw)
        {
            DrawLineInternal(dxf, layer, grid.GridStrokeColor, grid.GridStrokeThickness, gx, oy, gx, ey);
        }

        for (double gy = oy + ch; gy < ey; gy += ch)
        {
            DrawLineInternal(dxf, layer, grid.GridStrokeColor, grid.GridStrokeThickness, ox, gy, ex, gy);
        }
    }

    private void CreateHatchBoundsAndEntities(PathShapeViewModel pg, out IList<DXFE.HatchBoundaryPath> bounds, out ICollection<DXFE.EntityObject> entities)
    {
        bounds = new List<DXFE.HatchBoundaryPath>();
        entities = new List<DXFE.EntityObject>();

        // TODO: FillMode = pg.FillRule == FillRule.EvenOdd ? FillMode.Alternate : FillMode.Winding;

        foreach (var pf in pg.Figures)
        {
            var edges = new List<DXFE.EntityObject>();
            var startPoint = pf.StartPoint;

            foreach (var segment in pf.Segments)
            {
                if (segment is ArcSegmentViewModel arcSegment)
                {
                    throw new NotSupportedException("Not supported segment type: " + segment.GetType());
                    // TODO: Convert WPF/SVG elliptical arc segment format to DXF ellipse arc.
                    //startPoint = arcSegment.Point;
                }
                else if (segment is CubicBezierSegmentViewModel cubicBezierSegment)
                {
                    var dxfSpline = CreateCubicSpline(
                        startPoint.X,
                        startPoint.Y,
                        cubicBezierSegment.Point1.X,
                        cubicBezierSegment.Point1.Y,
                        cubicBezierSegment.Point2.X,
                        cubicBezierSegment.Point2.Y,
                        cubicBezierSegment.Point3.X,
                        cubicBezierSegment.Point3.Y);
                    edges.Add(dxfSpline);
                    entities.Add((DXFE.Spline)dxfSpline.Clone());
                    startPoint = cubicBezierSegment.Point3;
                }
                else if (segment is LineSegmentViewModel lineSegment)
                {
                    var dxfLine = CreateLine(
                        startPoint.X,
                        startPoint.Y,
                        lineSegment.Point.X,
                        lineSegment.Point.Y);
                    edges.Add(dxfLine);
                    entities.Add((DXFE.Line)dxfLine.Clone());
                    startPoint = lineSegment.Point;
                }
                else if (segment is QuadraticBezierSegmentViewModel quadraticBezierSegment)
                {
                    var dxfSpline = CreateQuadraticSpline(
                        startPoint.X,
                        startPoint.Y,
                        quadraticBezierSegment.Point1.X,
                        quadraticBezierSegment.Point1.Y,
                        quadraticBezierSegment.Point2.X,
                        quadraticBezierSegment.Point2.Y);
                    edges.Add(dxfSpline);
                    entities.Add((DXFE.Spline)dxfSpline.Clone());
                    startPoint = quadraticBezierSegment.Point2;
                }
                else
                {
                    throw new NotSupportedException("Not supported segment type: " + segment.GetType());
                }
            }

            // TODO: Add support for pf.IsClosed

            var path = new DXFE.HatchBoundaryPath(edges);
            bounds.Add(path);
        }
    }

    public void ClearCache()
    {
        _biCache.Reset();
    }

    public void Fill(object? dc, double x, double y, double width, double height, BaseColorViewModel? colorViewModel)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        FillRectangle(dxf, _currentLayer, x, y, width, height, colorViewModel);
    }

    public void Grid(object? dc, IGrid grid, double x, double y, double width, double height)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        var rect = Spatial.Rect2.FromPoints(
            x + grid.GridOffsetLeft,
            y + grid.GridOffsetTop,
            x + width - grid.GridOffsetLeft + grid.GridOffsetRight,
            y + height - grid.GridOffsetTop + grid.GridOffsetBottom,
            0, 0);

        if (grid.IsGridEnabled)
        {
            DrawGridInternal(
                dxf,
                _currentLayer,
                grid,
                ref rect);
        }

        if (grid.IsBorderEnabled)
        {
            StrokeRectangle(dxf, _currentLayer, grid.GridStrokeColor, grid.GridStrokeThickness, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    public void DrawPoint(object? dc, PointShapeViewModel point, ShapeStyleViewModel? style)
    {
        // TODO:
    }

    public void DrawLine(object? dc, LineShapeViewModel line, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        if (line.IsStroked)
        {
            double _x1 = line.Start.X;
            double _y1 = line.Start.Y;
            double _x2 = line.End.X;
            double _y2 = line.End.Y;

            // TODO: Draw line start arrow.

            // TODO: Draw line end arrow.

            DrawLineInternal(dxf, _currentLayer, style, line.IsStroked, _x1, _y1, _x2, _y2);
        }
    }

    public void DrawRectangle(object? dc, RectangleShapeViewModel rectangle, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        if (rectangle.IsStroked || rectangle.IsFilled)
        {
            var rect = Spatial.Rect2.FromPoints(
                rectangle.TopLeft.X,
                rectangle.TopLeft.Y,
                rectangle.BottomRight.X,
                rectangle.BottomRight.Y,
                0, 0);

            DrawRectangleInternal(dxf, _currentLayer, rectangle.IsFilled, rectangle.IsStroked, style, ref rect);
        }
    }

    public void DrawEllipse(object? dc, EllipseShapeViewModel ellipse, ShapeStyleViewModel? style)
    {
        if (ellipse.IsStroked || ellipse.IsFilled)
        {
            var dxf = dc as DXF.DxfDocument;
            var rect = Spatial.Rect2.FromPoints(
                ellipse.TopLeft.X,
                ellipse.TopLeft.Y,
                ellipse.BottomRight.X,
                ellipse.BottomRight.Y,
                0, 0);

            DrawEllipseInternal(dxf, _currentLayer, ellipse.IsFilled, ellipse.IsStroked, style, ref rect);
        }
    }

    public void DrawArc(object? dc, ArcShapeViewModel arc, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        var dxfEllipse = CreateEllipticalArc(arc);

        if (arc.IsFilled)
        {
            var fill = ToColor(style.Fill.Color);
            var fillTransparency = ToTransparency(style.Fill.Color);

            // TODO: The netDxf does not create hatch for Ellipse with end angle equal to 360.
            var bounds =
                new List<DXFE.HatchBoundaryPath>
                {
                    new DXFE.HatchBoundaryPath(
                        new List<DXFE.EntityObject>
                        {
                            (DXFE.Ellipse)dxfEllipse.Clone()
                        })
                };

            var hatch = new DXFE.Hatch(DXFE.HatchPattern.Solid, bounds, false)
            {
                Layer = _currentLayer,
                Color = fill
            };
            hatch.Transparency.Value = fillTransparency;

            dxf.AddEntity(hatch);
        }

        if (arc.IsStroked)
        {
            var stroke = ToColor(style.Stroke.Color);
            var strokeTansparency = ToTransparency(style.Stroke.Color);
            var lineweight = ToLineweight(style.Stroke.Thickness);

            dxfEllipse.Layer = _currentLayer;
            dxfEllipse.Color = stroke;
            dxfEllipse.Transparency.Value = strokeTansparency;
            dxfEllipse.Lineweight = lineweight;

            dxf.AddEntity(dxfEllipse);
        }
    }

    public void DrawCubicBezier(object? dc, CubicBezierShapeViewModel cubicBezier, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        if (cubicBezier.IsStroked || cubicBezier.IsFilled)
        {
            var dxfSpline = CreateCubicSpline(
                cubicBezier.Point1.X,
                cubicBezier.Point1.Y,
                cubicBezier.Point2.X,
                cubicBezier.Point2.Y,
                cubicBezier.Point3.X,
                cubicBezier.Point3.Y,
                cubicBezier.Point4.X,
                cubicBezier.Point4.Y);

            if (cubicBezier.IsFilled)
            {
                var fill = ToColor(style.Fill.Color);
                var fillTransparency = ToTransparency(style.Fill.Color);

                var bounds =
                    new List<DXFE.HatchBoundaryPath>
                    {
                        new DXFE.HatchBoundaryPath(
                            new List<DXFE.EntityObject>
                            {
                                (DXFE.Spline)dxfSpline.Clone()
                            })
                    };

                var hatch = new DXFE.Hatch(DXFE.HatchPattern.Solid, bounds, false)
                {
                    Layer = _currentLayer,
                    Color = fill
                };
                hatch.Transparency.Value = fillTransparency;

                dxf.AddEntity(hatch);
            }

            if (cubicBezier.IsStroked)
            {
                var stroke = ToColor(style.Stroke.Color);
                var strokeTansparency = ToTransparency(style.Stroke.Color);
                var lineweight = ToLineweight(style.Stroke.Thickness);

                dxfSpline.Layer = _currentLayer;
                dxfSpline.Color = stroke;
                dxfSpline.Transparency.Value = strokeTansparency;
                dxfSpline.Lineweight = lineweight;

                dxf.AddEntity(dxfSpline);
            }
        }
    }

    public void DrawQuadraticBezier(object? dc, QuadraticBezierShapeViewModel quadraticBezier, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        if (quadraticBezier.IsStroked || quadraticBezier.IsFilled)
        {
            var dxfSpline = CreateQuadraticSpline(
                quadraticBezier.Point1.X,
                quadraticBezier.Point1.Y,
                quadraticBezier.Point2.X,
                quadraticBezier.Point2.Y,
                quadraticBezier.Point3.X,
                quadraticBezier.Point3.Y);

            if (quadraticBezier.IsFilled)
            {
                var fill = ToColor(style.Fill.Color);
                var fillTransparency = ToTransparency(style.Fill.Color);

                var bounds =
                    new List<DXFE.HatchBoundaryPath>
                    {
                        new DXFE.HatchBoundaryPath(
                            new List<DXFE.EntityObject>
                            {
                                (DXFE.Spline)dxfSpline.Clone()
                            })
                    };

                var hatch = new DXFE.Hatch(DXFE.HatchPattern.Solid, bounds, false)
                {
                    Layer = _currentLayer,
                    Color = fill
                };
                hatch.Transparency.Value = fillTransparency;

                dxf.AddEntity(hatch);
            }

            if (quadraticBezier.IsStroked)
            {
                var stroke = ToColor(style.Stroke.Color);
                var strokeTansparency = ToTransparency(style.Stroke.Color);
                var lineweight = ToLineweight(style.Stroke.Thickness);

                dxfSpline.Layer = _currentLayer;
                dxfSpline.Color = stroke;
                dxfSpline.Transparency.Value = strokeTansparency;
                dxfSpline.Lineweight = lineweight;

                dxf.AddEntity(dxfSpline);
            }
        }
    }

    public void DrawText(object? dc, TextShapeViewModel text, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        if (!(text.GetProperty(nameof(TextShapeViewModel.Text)) is string tbind))
        {
            tbind = text.Text;
        }

        if (tbind is null)
        {
            return;
        }

        var stroke = ToColor(style.Stroke.Color);
        var strokeTansparency = ToTransparency(style.Stroke.Color);

        var attachmentPoint = default(DXFE.MTextAttachmentPoint);
        var rect = Spatial.Rect2.FromPoints(
            text.TopLeft.X,
            text.TopLeft.Y,
            text.BottomRight.X,
            text.BottomRight.Y,
            0, 0);
        var x = style.TextStyle.TextHAlignment switch
        {
            TextHAlignment.Center => rect.X + rect.Width / 2.0,
            TextHAlignment.Right => rect.X + rect.Width,
            _ => rect.X,
        };
        var y = style.TextStyle.TextVAlignment switch
        {
            TextVAlignment.Center => rect.Y + rect.Height / 2.0,
            TextVAlignment.Bottom => rect.Y + rect.Height,
            _ => rect.Y,
        };
        attachmentPoint = style.TextStyle.TextVAlignment switch
        {
            TextVAlignment.Center => style.TextStyle.TextHAlignment switch
            {
                TextHAlignment.Center => DXFE.MTextAttachmentPoint.MiddleCenter,
                TextHAlignment.Right => DXFE.MTextAttachmentPoint.MiddleRight,
                _ => DXFE.MTextAttachmentPoint.MiddleLeft,
            },
            TextVAlignment.Bottom => style.TextStyle.TextHAlignment switch
            {
                TextHAlignment.Center => DXFE.MTextAttachmentPoint.BottomCenter,
                TextHAlignment.Right => DXFE.MTextAttachmentPoint.BottomRight,
                _ => DXFE.MTextAttachmentPoint.BottomLeft,
            },
            _ => style.TextStyle.TextHAlignment switch
            {
                TextHAlignment.Center => DXFE.MTextAttachmentPoint.TopCenter,
                TextHAlignment.Right => DXFE.MTextAttachmentPoint.TopRight,
                _ => DXFE.MTextAttachmentPoint.TopLeft,
            },
        };
        var ts = new netDxf.Tables.TextStyle(style.TextStyle.FontName, style.TextStyle.FontFile);
        var dxfMText = new DXFE.MText(
            new DXF.Vector3(ToDxfX(x), ToDxfY(y), 0),
            style.TextStyle.FontSize * _targetDpi / _sourceDpi,
            rect.Width,
            ts)
        {
            AttachmentPoint = attachmentPoint
        };

        var options = new DXFE.MTextFormattingOptions();
        var fs = style.TextStyle.FontStyle;

        options.Bold = fs.HasFlag(FontStyleFlags.Bold);
        options.Italic = fs.HasFlag(FontStyleFlags.Italic);

        options.Color = null;
        dxfMText.Write(tbind, options);

        dxfMText.Layer = _currentLayer;
        dxfMText.Transparency.Value = strokeTansparency;
        dxfMText.Color = stroke;

        dxf.AddEntity(dxfMText);
    }

    public void DrawImage(object? dc, ImageShapeViewModel image, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        var bytes = State.ImageCache.GetImage(image.Key);
        if (bytes is { })
        {
            var rect = Spatial.Rect2.FromPoints(
                image.TopLeft.X,
                image.TopLeft.Y,
                image.BottomRight.X,
                image.BottomRight.Y,
                0, 0);

            var dxfImageDefinitionCached = _biCache.Get(image.Key);
            if (dxfImageDefinitionCached is { })
            {
                var dxfImage = new DXFE.Image(
                    dxfImageDefinitionCached,
                    new DXF.Vector3(ToDxfX(rect.X), ToDxfY(rect.Y + rect.Height), 0),
                    rect.Width,
                    rect.Height);
                dxf.AddEntity(dxfImage);
            }
            else
            {
                if (State.ImageCache is { } && !string.IsNullOrEmpty(image.Key) && !string.IsNullOrEmpty(_outputPath))
                {
                    var path = System.IO.Path.Combine(_outputPath, System.IO.Path.GetFileName(image.Key));
                    System.IO.File.WriteAllBytes(path, bytes);
                    var dxfImageDefinition = new DXFO.ImageDefinition(path);

                    _biCache.Set(image.Key, dxfImageDefinition);

                    var dxfImage = new DXFE.Image(
                        dxfImageDefinition,
                        new DXF.Vector3(ToDxfX(rect.X), ToDxfY(rect.Y + rect.Height), 0),
                        rect.Width,
                        rect.Height);
                    dxf.AddEntity(dxfImage);
                }
            }
        }
    }

    public void DrawPath(object? dc, PathShapeViewModel path, ShapeStyleViewModel? style)
    {
        if (dc is not DXF.DxfDocument dxf)
        {
            return;
        }

        if (path.IsStroked || path.IsFilled)
        {
            CreateHatchBoundsAndEntities(path, out var bounds, out var entities);
            if (entities is null || bounds is null)
            {
                return;
            }

            if (path.IsFilled)
            {
                var fill = ToColor(style.Fill.Color);
                var fillTransparency = ToTransparency(style.Fill.Color);

                var hatch = new DXFE.Hatch(DXFE.HatchPattern.Solid, bounds, false)
                {
                    Layer = _currentLayer,
                    Color = fill
                };
                hatch.Transparency.Value = fillTransparency;

                dxf.AddEntity(hatch);
            }

            if (path.IsStroked)
            {
                // TODO: Add support for Closed paths.

                var stroke = ToColor(style.Stroke.Color);
                var strokeTansparency = ToTransparency(style.Stroke.Color);
                var lineweight = ToLineweight(style.Stroke.Thickness);

                foreach (var entity in entities)
                {
                    entity.Layer = _currentLayer;
                    entity.Color = stroke;
                    entity.Transparency.Value = strokeTansparency;
                    entity.Lineweight = lineweight;
                    dxf.AddEntity(entity);
                }
            }
        }
    }
}
