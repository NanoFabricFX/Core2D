﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Core2D.Shapes;

namespace Core2D.Path.Segments
{
    /// <summary>
    /// Arc path segment.
    /// </summary>
    public class ArcSegment : PathSegment
    {
        private PointShape _point;
        private PathSize _size;
        private double _rotationAngle;
        private bool _isLargeArc;
        private SweepDirection _sweepDirection;

        /// <inheritdoc/>
        public PointShape Point
        {
            get => _point;
            set => RaiseAndSetIfChanged(ref _point, value);
        }

        /// <inheritdoc/>
        public PathSize Size
        {
            get => _size;
            set => RaiseAndSetIfChanged(ref _size, value);
        }

        /// <inheritdoc/>
        public double RotationAngle
        {
            get => _rotationAngle;
            set => RaiseAndSetIfChanged(ref _rotationAngle, value);
        }

        /// <inheritdoc/>
        public bool IsLargeArc
        {
            get => _isLargeArc;
            set => RaiseAndSetIfChanged(ref _isLargeArc, value);
        }

        /// <inheritdoc/>
        public SweepDirection SweepDirection
        {
            get => _sweepDirection;
            set => RaiseAndSetIfChanged(ref _sweepDirection, value);
        }

        /// <inheritdoc/>
        public override void GetPoints(IList<PointShape> points)
        {
            points.Add(Point);
        }

        /// <inheritdoc/>
        public override object Copy(IDictionary<object, object> shared)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override bool IsDirty()
        {
            var isDirty = base.IsDirty();

            isDirty |= Point.IsDirty();
            isDirty |= Size.IsDirty();

            return isDirty;
        }

        /// <inheritdoc/>
        public override void Invalidate()
        {
            base.Invalidate();

            Point.Invalidate();
            Size.Invalidate();
        }

        /// <inheritdoc/>
        public override string ToXamlString()
            => $"A{Size.ToXamlString()} {RotationAngle.ToString(CultureInfo.InvariantCulture)} {(IsLargeArc ? "1" : "0")} {(SweepDirection == SweepDirection.Clockwise ? "1" : "0")} {Point.ToXamlString()}";

        public override string ToSvgString()
            => $"A{Size.ToSvgString()} {RotationAngle.ToString(CultureInfo.InvariantCulture)} {(IsLargeArc ? "1" : "0")} {(SweepDirection == SweepDirection.Clockwise ? "1" : "0")} {Point.ToSvgString()}";

        /// <summary>
        /// Check whether the <see cref="Point"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializePoint() => _point != null;

        /// <summary>
        /// Check whether the <see cref="Size"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeSize() => _size != null;

        /// <summary>
        /// Check whether the <see cref="RotationAngle"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeRotationAngle() => _rotationAngle != default;

        /// <summary>
        /// Check whether the <see cref="IsLargeArc"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeIsLargeArc() => _isLargeArc != default;

        /// <summary>
        /// Check whether the <see cref="SweepDirection"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeSweepDirection() => _sweepDirection != default;
    }
}
