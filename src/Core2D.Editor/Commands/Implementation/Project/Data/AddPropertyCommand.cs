﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Core2D.Data;
using Core2D.Editor.Input;

namespace Core2D.Editor.Commands
{
    /// <inheritdoc/>
    public class AddPropertyCommand : Command<Data.Context>, IAddPropertyCommand
    {
        /// <inheritdoc/>
        public override bool CanRun(Data.Context data)
            => ServiceProvider.GetService<ProjectEditor>().IsEditMode();

        /// <inheritdoc/>
        public override void Run(Data.Context data)
            => ServiceProvider.GetService<ProjectEditor>().OnAddProperty(data);
    }
}
