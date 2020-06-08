﻿using System;
using Core2D.Shapes;
using Core2D.Style;
using SkiaSharp;

namespace Core2D.Renderer.SkiaSharp
{
    internal static class SkiaSharpDrawUtil
    {
        public static SKRect ToSKRect(double x, double y, double width, double height)
        {
            float left = (float)x;
            float top = (float)y;
            float right = (float)(x + width);
            float bottom = (float)(y + height);
            return new SKRect(left, top, right, bottom);
        }

        public static SKRect CreateRect(IPointShape tl, IPointShape br)
        {
            float left = (float)Math.Min(tl.X, br.X);
            float top = (float)Math.Min(tl.Y, br.Y);
            float right = (float)Math.Max(tl.X, br.X);
            float bottom = (float)Math.Max(tl.Y, br.Y);
            return new SKRect(left, top, right, bottom);
        }
        public static SKColor ToSKColor(IColor color)
        {
            return color switch
            {
                IArgbColor argbColor => new SKColor(argbColor.R, argbColor.G, argbColor.B, argbColor.A),
                _ => throw new NotSupportedException($"The {color.GetType()} color type is not supported."),
            };
        }

        public static SKPaint ToSKPaintBrush(IColor color)
        {
            var brush = new SKPaint();

            brush.Style = SKPaintStyle.Fill;
            brush.IsAntialias = true;
            brush.IsStroke = false;
            brush.LcdRenderText = true;
            brush.SubpixelText = true;
            brush.Color = ToSKColor(color);

            return brush;
        }

        public static SKStrokeCap ToStrokeCap(IBaseStyle style)
        {
            return style.LineCap switch
            {
                LineCap.Square => SKStrokeCap.Square,
                LineCap.Round => SKStrokeCap.Round,
                _ => SKStrokeCap.Butt,
            };
        }

        public static SKPaint ToSKPaintPen(IBaseStyle style, double strokeWidth)
        {
            var pen = new SKPaint();

            var pathEffect = default(SKPathEffect);
            if (style.Dashes != null)
            {
                var intervals = StyleHelper.ConvertDashesToFloatArray(style.Dashes, strokeWidth);
                var phase = (float)(style.DashOffset * strokeWidth);
                if (intervals != null)
                {
                    pathEffect = SKPathEffect.CreateDash(intervals, phase);
                }
            }

            pen.Style = SKPaintStyle.Stroke;
            pen.IsAntialias = true;
            pen.IsStroke = true;
            pen.StrokeWidth = (float)strokeWidth;
            pen.Color = ToSKColor(style.Stroke);
            pen.StrokeCap = ToStrokeCap(style);
            pen.PathEffect = pathEffect;

            return pen;
        }

        public static SKPoint GetTextOrigin(IShapeStyle style, ref SKRect rect, ref SKRect size)
        {
            double rwidth = Math.Abs(rect.Right - rect.Left);
            double rheight = Math.Abs(rect.Bottom - rect.Top);
            double swidth = Math.Abs(size.Right - size.Left);
            double sheight = Math.Abs(size.Bottom - size.Top);
            var ox = style.TextStyle.TextHAlignment switch
            {
                TextHAlignment.Left => rect.Left,
                TextHAlignment.Right => rect.Right - swidth,
                _ => (rect.Left + rwidth / 2f) - (swidth / 2f),
            };
            var oy = style.TextStyle.TextVAlignment switch
            {
                TextVAlignment.Top => rect.Top,
                TextVAlignment.Bottom => rect.Bottom - sheight,
                _ => (rect.Bottom - rheight / 2f) - (sheight / 2f),
            };
            return new SKPoint((float)ox, (float)oy);
        }

        public static SKPaint GetSKPaint(string text, IShapeStyle shapeStyle, IPointShape topLeft, IPointShape bottomRight, out SKPoint origin)
        {
            var pen = ToSKPaintBrush(shapeStyle.Stroke);

            var weight = SKFontStyleWeight.Normal;
            if (shapeStyle.TextStyle.FontStyle != null)
            {
                if (shapeStyle.TextStyle.FontStyle.Flags.HasFlag(FontStyleFlags.Bold))
                {
                    weight |= SKFontStyleWeight.Bold;
                }
            }

            var style = SKFontStyleSlant.Upright;
            if (shapeStyle.TextStyle.FontStyle != null)
            {
                if (shapeStyle.TextStyle.FontStyle.Flags.HasFlag(FontStyleFlags.Italic))
                {
                    style |= SKFontStyleSlant.Italic;
                }
            }

            var tf = SKTypeface.FromFamilyName(shapeStyle.TextStyle.FontName, weight, SKFontStyleWidth.Normal, style);
            pen.Typeface = tf;
            pen.TextEncoding = SKTextEncoding.Utf16;
            pen.TextSize = (float)(shapeStyle.TextStyle.FontSize);

            pen.TextAlign = shapeStyle.TextStyle.TextHAlignment switch
            {
                TextHAlignment.Center => SKTextAlign.Center,
                TextHAlignment.Right => SKTextAlign.Right,
                _ => SKTextAlign.Left,
            };

            var metrics = pen.FontMetrics;
            var mAscent = metrics.Ascent;
            var mDescent = metrics.Descent;
            var rect = CreateRect(topLeft, bottomRight);
            float x = rect.Left;
            float y = rect.Top;
            float width = rect.Width;
            float height = rect.Height;

            switch (shapeStyle.TextStyle.TextVAlignment)
            {
                default:
                case TextVAlignment.Top:
                    y -= mAscent;
                    break;
                case TextVAlignment.Center:
                    y += (height / 2.0f) - (mAscent / 2.0f) - mDescent / 2.0f;
                    break;
                case TextVAlignment.Bottom:
                    y += height - mDescent;
                    break;
            }

            switch (shapeStyle.TextStyle.TextHAlignment)
            {
                default:
                case TextHAlignment.Left:
                    // x = x;
                    break;
                case TextHAlignment.Center:
                    x += width / 2.0f;
                    break;
                case TextHAlignment.Right:
                    x += width;
                    break;
            }

            origin = new SKPoint(x, y);

            return pen;
        }
    }
}
