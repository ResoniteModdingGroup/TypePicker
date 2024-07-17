using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using MonkeyLoader.Resonite;
using ComponentSelectorAdditions.Events;
using MonkeyLoader.Patching;
using Elements.Core;
using MonkeyLoader;
using MonkeyLoader.Resonite.UI;

namespace TypePicker
{
    internal sealed class TypePickerHandler : ResoniteEventHandlerMonkey<TypePickerHandler, BuildCustomGenericBuilder>
    {
        public override bool CanBeDisabled => true;
        public override int Priority => HarmonyLib.Priority.Low;

        protected override bool AppliesTo(BuildCustomGenericBuilder eventData) => Enabled;

        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        protected override void Handle(BuildCustomGenericBuilder eventData)
            => eventData.Selector.RunInUpdates(0, () => AddTypePickerUI(eventData.Selector, eventData.UI));

        private static Type? CastToIField(ReferenceField<IWorldElement> refField)
        {
            if (refField.Reference.Target is IField field)
                return typeof(IField<>).MakeGenericType(field.ValueType);

            return null;
        }

        private static Type? CastToIFieldInner(ReferenceField<IWorldElement> refField)
            => refField.Reference.Target is IField field ? field.ValueType : null;

        private static Type? CastToSyncRef(ReferenceField<IWorldElement> refField)
        {
            if (FindBaseType(refField) is not Type baseType)
                return null;

            if (refField.Reference.Target is ISyncRef syncRef)
                return typeof(ISyncRef<>).MakeGenericType(syncRef.TargetType);

            var syncRefType = typeof(ISyncRef<>).MakeGenericType(baseType);
            if (syncRefType.IsValidGenericType(true))
                return syncRefType;

            return null;
        }

        private static Type? CastToSyncRefInner(ReferenceField<IWorldElement> refField)
            => refField.Reference.Target is ISyncRef syncRef ? syncRef.TargetType : null;

        private static Type? FindBaseType(ReferenceField<IWorldElement> refField)
            => refField.Reference.Target?.GetType();

        private static Type? FindInnerType(ReferenceField<IWorldElement> refField)
        {
            var baseType = FindBaseType(refField);
            return baseType?.IsConstructedGenericType ?? false ? baseType?.GenericTypeArguments[0] : null;
        }

        private static void SetType(TextField field, Type? type)
        {
            if (type is null)
            {
                field.Editor.Target.Text.Target.Text = null;
                return;
            }

            try
            {
                field.Editor.Target.Text.Target.Text = field.World.Types.EncodeType(type);
            }
            catch (Exception ex)
            {
                Logger.Warn(() => ex.Format($"Exception while trying to encode type: {type?.CompactDescription()}"));
                field.Editor.Target.Text.Target.Text = null;
            }
        }

        private void AddTypePickerUI(ComponentSelector selector, UIBuilder ui)
        {
            var root = ui.Root;
            ui.NestInto(selector._uiRoot);
            var fields = selector._customGenericArguments;

            if (ui.Root[0].GetComponentInChildren<ButtonRelay<string>>()?.ButtonPressed.Target == selector.OnOpenCategoryPressed)
                ui.Root[0].OrderOffset = -200;

            ui.PushStyle();
            ui.Style.FlexibleWidth = 1;
            ui.Style.MinHeight = 150;

            var pickerPanel = ui.Panel();
            pickerPanel.Slot.OrderOffset = -100;
            pickerPanel.Slot.DestroyWhenLocalUserLeaves();

            ui.Style.MinHeight = 32;
            ui.Style.PreferredHeight = 32;
            ui.Style.FlexibleHeight = -1;

            ui.VerticalLayout(4, 0, Alignment.TopCenter, false, false);

            var refField = pickerPanel.Slot.AttachComponent<ReferenceField<IWorldElement>>();

            // max amount of generic params is ushort.MaxValue (I checked this myself)
            var paramIndex = pickerPanel.Slot.AttachComponent<ValueField<ushort>>().Value;

            // Make the Generic Param Name clickable to select which one you put into.
            for (var i = 0; i < fields.Count; ++i)
            {
                var field = fields[i];
                var textSlot = field.Slot.Parent.Parent[0][0];

                var btn = textSlot.AttachComponent<Button>();
                var text = textSlot.GetComponent<Text>();
                btn.SetupBackgroundColor(text.Color);
                btn.ColorDrivers.FirstOrDefault().DisabledColor.Value = RadiantUI_Constants.Hero.GREEN;

                var radio = textSlot.AttachComponent<ValueRadio<ushort>>();
                radio.OptionValue.Value = (ushort)i;
                radio.TargetValue.Target = paramIndex;

                var enabledDriver = textSlot.AttachComponent<BooleanValueDriver<bool>>();
                enabledDriver.FalseValue.Value = true;
                enabledDriver.TrueValue.Value = false;
                enabledDriver.TargetField.Target = btn.EnabledField;
                radio.CheckVisual.Target = enabledDriver.State;

                var italicDriver = textSlot.AttachComponent<BooleanValueDriver<string>>();
                italicDriver.FalseValue.Value = text.Content;
                italicDriver.TrueValue.Value = $"<i>{text.Content}</i>";
                italicDriver.TargetField.Target = text.Content;
                italicDriver.State.DriveFrom(enabledDriver.State);
            }

            SyncMemberEditorBuilder.Build(refField.Reference, "Type Picker", refField.GetSyncMemberFieldInfo(nameof(ReferenceField<SyncRef>.Reference)), ui);

            var layout = ui.HorizontalLayout(8);
            layout.ForceExpand = false;

            ui.LocalActionButton(Mod.GetLocaleString("BaseType"), _ => SetType(fields[paramIndex.Value], FindBaseType(refField)));
            ui.LocalActionButton(Mod.GetLocaleString("InnerType"), _ => SetType(fields[paramIndex.Value], FindInnerType(refField)));

            ui.NestOut();

            ui.Text(Mod.GetLocaleString("CastTo"));

            layout = ui.HorizontalLayout(8);
            layout.ForceExpand = false;

            ui.LocalActionButton(Mod.GetLocaleString("SyncRef"), _ => SetType(fields[paramIndex.Value], CastToSyncRef(refField)));
            ui.LocalActionButton(Mod.GetLocaleString("SyncRefInner"), _ => SetType(fields[paramIndex.Value], CastToSyncRefInner(refField)));
            ui.LocalActionButton(Mod.GetLocaleString("Field"), _ => SetType(fields[paramIndex.Value], CastToIField(refField)));
            ui.LocalActionButton(Mod.GetLocaleString("FieldInner"), _ => SetType(fields[paramIndex.Value], CastToIFieldInner(refField)));

            ui.NestOut();

            ui.PopStyle();
            ui.NestInto(root);
        }
    }
}