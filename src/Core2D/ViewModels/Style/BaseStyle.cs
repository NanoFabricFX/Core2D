﻿namespace Core2D.Style
{
    /// <summary>
    /// Base style.
    /// </summary>
    public abstract class BaseStyle : ObservableObject
    {
        private BaseColor _stroke;
        private BaseColor _fill;
        private double _thickness;
        private LineCap _lineCap;
        private string _dashes;
        private double _dashOffset;

        /// <inheritdoc/>
        public BaseColor Stroke
        {
            get => _stroke;
            set => RaiseAndSetIfChanged(ref _stroke, value);
        }

        /// <inheritdoc/>
        public BaseColor Fill
        {
            get => _fill;
            set => RaiseAndSetIfChanged(ref _fill, value);
        }

        /// <inheritdoc/>
        public double Thickness
        {
            get => _thickness;
            set => RaiseAndSetIfChanged(ref _thickness, value);
        }

        /// <inheritdoc/>
        public LineCap LineCap
        {
            get => _lineCap;
            set => RaiseAndSetIfChanged(ref _lineCap, value);
        }

        /// <inheritdoc/>
        public string Dashes
        {
            get => _dashes;
            set => RaiseAndSetIfChanged(ref _dashes, value);
        }

        /// <inheritdoc/>
        public double DashOffset
        {
            get => _dashOffset;
            set => RaiseAndSetIfChanged(ref _dashOffset, value);
        }

        /// <inheritdoc/>
        public override bool IsDirty()
        {
            var isDirty = base.IsDirty();

            isDirty |= Stroke.IsDirty();
            isDirty |= Fill.IsDirty();

            return isDirty;
        }

        /// <inheritdoc/>
        public override void Invalidate()
        {
            base.Invalidate();
            Stroke.Invalidate();
            Fill.Invalidate();
        }

        /// <summary>
        /// Check whether the <see cref="Stroke"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeStroke() => _stroke != null;

        /// <summary>
        /// Check whether the <see cref="Fill"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeFill() => _fill != null;

        /// <summary>
        /// Check whether the <see cref="Thickness"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeThickness() => _thickness != default;

        /// <summary>
        /// Check whether the <see cref="LineCap"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeLineCap() => _lineCap != default;

        /// <summary>
        /// Check whether the <see cref="Dashes"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeDashes() => !string.IsNullOrWhiteSpace(_dashes);

        /// <summary>
        /// Check whether the <see cref="DashOffset"/> property has changed from its default value.
        /// </summary>
        /// <returns>Returns true if the property has changed; otherwise, returns false.</returns>
        public virtual bool ShouldSerializeDashOffset() => _dashOffset != default;
    }
}
