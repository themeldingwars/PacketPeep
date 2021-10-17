using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Aero.Gen;
using Aero.Gen.Attributes;
using ImGuiNET;
using ImTool;

namespace PacketPeep.Widgets
{
    public class AeroInspector
    {
        public IAero                    AeroObj;
        public List<AeroInspectorEntry> Entries      = new();
        public int                      HoveredIdx   = -1;
        public AeroInspectorEntry       HoveredEntry = null;
        public Action<string>           LogError;

        private int OrderIdx;
        private int Offset;

        // Display settings
        private const int COLOR_BAR_WIDTH = 10;
        private const int LINE_HEIGHT     = 25;
        private const int INDENT_DIST     = 5;

        public AeroInspector(IAero aeroObj, Action<string> logError)
        {
            AeroObj  = aeroObj;
            LogError = logError;
            BuildData();
        }

        public void BuildData()
        {
            if (AeroObj == null) return;

            var type = AeroObj.GetType();
            OrderIdx = 0;

            AddEntriesForType(type, AeroObj);
        }

        private int GetSizeFromTypeName(string typeName)
        {
            int size = 0;
            if (typeName.StartsWith("System.Numerics")) {
                size = Genv2.GetTypeSize(typeName);
            }
            else {
                var name = typeName
                          .Replace("System.", "")
                          .Replace("Single", "float");
                size = Genv2.GetTypeSize(name);
            }

            return size > 0 ? size : 0;
        }

        // Add logic for ifs
        private void AddEntriesForType(Type type, object obj, AeroInspectorEntry parentEntry = null)
        {
            try {
                foreach (var f in type.GetFields().Where(f => f.IsPublic)) {
                    if (ChecKAeroIf(f)) {
                        var entry = new AeroInspectorEntry
                        {
                            Name     = f.Name,
                            EType    = GetEntryTypeFromType(f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType),
                            IsArray  = f.FieldType.IsArray,
                            Ref      = f,
                            OrderIdx = OrderIdx++,
                            Size     = f.FieldType.IsArray ? 0 : GetSizeFromTypeName(f.FieldType.IsEnum ? Enum.GetUnderlyingType(f.FieldType).FullName : f.FieldType.FullName),
                            Offset   = Offset,
                            ColorIdx = OrderIdx % Config.Inst.MessageEntryColors.Count,
                            Obj      = obj,
                            Parent   = parentEntry
                        };

                        if (entry.EType == AeroInspectorEntry.EntryType.String && !entry.IsArray) {
                            entry.Size = ((string) f.GetValue(obj)).Length;

                            // if the string is null terminated add 1 for the 0 at the end
                            var stringArrtib = f.GetCustomAttribute<AeroStringAttribute>();
                            if (stringArrtib != null && stringArrtib.Length == 0 && stringArrtib.LengthStr == null && stringArrtib.LengthType == null) {
                                entry.Size += 1;
                            }
                        }

                        Offset += entry.Size;

                        if (entry.IsArray) {
                            var arr = ((Array) f.GetValue(obj));
                            if (arr != null) {
                                // If the array has a length prefixx add that offset
                                var arrayAttr = f.GetCustomAttribute<AeroArrayAttribute>();
                                if (arrayAttr is {Typ: { }}) {
                                    var size = GetSizeFromTypeName(arrayAttr.Typ.FullName);
                                    Offset += size;
                                }

                                for (int i = 0; i < arr.Length; i++) {
                                    var val = arr.GetValue(i);

                                    var entry2 = new AeroInspectorEntry
                                    {
                                        Name     = $"[{i}]",
                                        EType    = GetEntryTypeFromType(val.GetType()),
                                        IsArray  = val.GetType().IsArray,
                                        Ref      = f,
                                        OrderIdx = OrderIdx++,
                                        Parent   = entry,
                                        Size     = GetSizeFromTypeName(f.FieldType.IsArray ? f.FieldType.GetElementType()?.FullName : f.FieldType.FullName),
                                        Offset   = Offset,
                                        ColorIdx = entry.ColorIdx,
                                        Obj      = f.GetValue(obj)
                                    };

                                    Offset += entry2.Size;

                                    if (entry2.EType == AeroInspectorEntry.EntryType.AeroBlock) {
                                        AddEntriesForType(f.FieldType.GetElementType(), val, entry2);
                                    }

                                    entry.SubEntrys.Add(entry2);
                                }
                            }

                            entry.Size = Offset - entry.Offset;
                        }
                        else if (entry.EType == AeroInspectorEntry.EntryType.AeroBlock) {
                            AddEntriesForType(f.FieldType, f.GetValue(obj), entry);
                        }

                        if (parentEntry == null) {
                            Entries.Add(entry);
                        }
                        else {
                            parentEntry.SubEntrys.Add(entry);
                        }
                    }
                }
            }
            catch (Exception e) {
                LogError?.Invoke($"Error building inspection tree for {obj}, Error: {e}");
            }
        }

        // If this field has an iff check the logic, if it doesn't it passes by default
        private bool ChecKAeroIf(FieldInfo f)
        {
            var attrs = f.GetCustomAttributes<AeroIfAttribute>();
            foreach (var attr in attrs) {
                var keyValue = Entries.FindLast(x => x.Name == attr.Key);
                if (keyValue != null) {
                    var keyType = keyValue.Ref.FieldType.IsArray ? keyValue.Ref.FieldType.GetElementType() : keyValue.Ref.FieldType;
                    if (attr.Op == AeroIfAttribute.Ops.Equal) {
                        if (!attr.Values.All(x => Convert.ChangeType(x, keyType).Equals(Convert.ChangeType(keyValue.GetValue<object>(), keyType)))) {
                            return false;
                        }
                    }
                    else if (attr.Op == AeroIfAttribute.Ops.NotEqual) {
                        if (attr.Values.All(x => Convert.ChangeType(x, keyType).Equals(Convert.ChangeType(keyValue.GetValue<object>(), keyType)))) {
                            return false;
                        }
                    }
                    else if (attr.Op == AeroIfAttribute.Ops.HasFlag) {
                        if (!attr.Values.All(x => (Convert.ChangeType(keyValue.GetValue<object>(), keyType) as Enum).HasFlag((Enum) Convert.ChangeType(x, keyType)))) {
                            return false;
                        }
                    }
                    else if (attr.Op == AeroIfAttribute.Ops.DoesntHaveFlag) {
                        if (attr.Values.All(x => (Convert.ChangeType(keyValue.GetValue<object>(), keyType) as Enum).HasFlag((Enum) Convert.ChangeType(x, keyType)))) {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void Draw()
        {
            if (ImGui.BeginTable("Inspector Table", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoSort, 0.5f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None, 0.5f);
                ImGui.TableHeadersRow();

                var indentLevel = 0;
                foreach (var entry in Entries) {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, LINE_HEIGHT);
                    DrawEntry(entry, ref indentLevel);
                    indentLevel = 0;
                }

                ImGui.EndTable();
            }
        }

        private bool DrawEntry(AeroInspectorEntry entry, ref int indentLevel)
        {
            ImGui.PushID($"Entry_{entry.OrderIdx}");

            // Color label and name
            ImGui.TableNextColumn();
            var textHeight     = ImGui.GetTextLineHeight();
            var drawList       = ImGui.GetWindowDrawList();
            var pos            = ImGui.GetCursorScreenPos() - new Vector2(5 - (indentLevel * INDENT_DIST), 1);
            var isHovered      = ImGui.IsMouseHoveringRect(pos, pos + new Vector2(ImGui.GetColumnWidth(), LINE_HEIGHT));
            var colorBarEndPos = pos + new Vector2(isHovered ? COLOR_BAR_WIDTH : COLOR_BAR_WIDTH / 2, LINE_HEIGHT - 2);
            drawList.AddRectFilled
            (
                pos,
                colorBarEndPos,
                ImGui.ColorConvertFloat4ToU32(Config.Inst.MessageEntryColors[$"Color {entry.ColorIdx}"])
            );

            ImGui.SetCursorScreenPos(new Vector2(colorBarEndPos.X + 5, (pos.Y) + textHeight / 2));
            //ImGui.Text(entry.Name);
            ImGui.Text($"{entry.Name}");

            if (isHovered) {
                HoveredIdx   = entry.OrderIdx;
                HoveredEntry = entry;

                ImGui.BeginTooltip();
                ImGui.Text($"Type: {entry.Ref.FieldType}");
                ImGui.Text($"Offset: {entry.Offset}");
                ImGui.Text($"Size: {entry.Size}");
                ImGui.EndTooltip();
            }

            // Value display
            ImGui.TableNextColumn();
            if (entry.EType == AeroInspectorEntry.EntryType.Unknown) {
                ImGui.Text($"Name: {entry.Name}, EType: {entry.EType}, OrderIdx: {entry.OrderIdx}");
                ImGui.PopID();
                return false;
            }
            else if (entry.IsArray || entry.EType == AeroInspectorEntry.EntryType.AeroBlock) {
                var state           = ImGui.GetStateStorage();
                var expandedStateId = ImGui.GetID("expanded");
                var isExpaneded     = state.GetBool(expandedStateId);
                if (ImGui.Button(isExpaneded ? $"Collapse ({entry.SubEntrys.Count})" : $"Expand ({entry.SubEntrys.Count})", new Vector2(-1, 0))) {
                    state.SetBool(expandedStateId, !isExpaneded);
                }

                if (isExpaneded) {
                    indentLevel++;
                    var hasChanged = false;
                    foreach (var subEntry in entry.SubEntrys) {
                        hasChanged = DrawEntry(subEntry, ref indentLevel);
                    }

                    indentLevel--;
                }
            }
            else {
                var draw = GetDisplayFunc(entry);

                ImGui.SetNextItemWidth(-1);
                var hasChanged = draw(entry);
                ImGui.PopID();
                return hasChanged;
            }

            ImGui.PopID();
            return false;
        }

        private Func<AeroInspectorEntry, bool> GetDisplayFunc(AeroInspectorEntry entry)
        {
            Func<AeroInspectorEntry, bool> draw = entry.EType switch
            {
                AeroInspectorEntry.EntryType.Int    => DrawInt,
                AeroInspectorEntry.EntryType.Long   => DrawLong,
                AeroInspectorEntry.EntryType.Ulong  => DrawULong,
                AeroInspectorEntry.EntryType.Short  => DrawShort,
                AeroInspectorEntry.EntryType.Ushort => DrawUShort,
                AeroInspectorEntry.EntryType.Uint   => DrawUInt,
                AeroInspectorEntry.EntryType.Byte   => DrawByte,
                AeroInspectorEntry.EntryType.Char   => DrawByte,
                AeroInspectorEntry.EntryType.Float  => DrawFloat,
                AeroInspectorEntry.EntryType.Double => DrawDouble,

                AeroInspectorEntry.EntryType.Vector2    => DrawVector2,
                AeroInspectorEntry.EntryType.Vector3    => DrawVector3,
                AeroInspectorEntry.EntryType.Vector4    => DrawVector4,
                AeroInspectorEntry.EntryType.Quaternion => DrawQuaternion,

                AeroInspectorEntry.EntryType.String => DrawText,

                _ => null
            };

            return draw ?? DrawUnknown;
        }

        // Draw number types
        private unsafe bool DrawNumber<T>(AeroInspectorEntry entry, ImGuiDataType dataType) where T : unmanaged
        {
            var val        = entry.GetValue<T>();
            var hasChanged = ImGui.InputScalar($"###{entry.Name}", dataType, (IntPtr) (&val));
            if (hasChanged) entry.SetValue(val);

            if (ImGui.IsItemHovered() && entry.Parent is {IsArray: false}) {
                ImGui.BeginTooltip();
                var flagsSet = entry.Ref.GetValue(entry.Obj)?.ToString()?.Replace("|", "\n").Replace(",", "\n");
                if (flagsSet != null) {
                    ImGui.Text($"{flagsSet}");
                }

                ImGui.EndTooltip();
            }

            return hasChanged;
        }

        private bool DrawByte(AeroInspectorEntry   entry) => DrawNumber<byte>(entry, ImGuiDataType.U8);
        private bool DrawInt(AeroInspectorEntry    entry) => DrawNumber<int>(entry, ImGuiDataType.S32);
        private bool DrawUInt(AeroInspectorEntry   entry) => DrawNumber<uint>(entry, ImGuiDataType.U32);
        private bool DrawLong(AeroInspectorEntry   entry) => DrawNumber<long>(entry, ImGuiDataType.S64);
        private bool DrawULong(AeroInspectorEntry  entry) => DrawNumber<ulong>(entry, ImGuiDataType.U64);
        private bool DrawShort(AeroInspectorEntry  entry) => DrawNumber<short>(entry, ImGuiDataType.S16);
        private bool DrawUShort(AeroInspectorEntry entry) => DrawNumber<ushort>(entry, ImGuiDataType.U16);
        private bool DrawFloat(AeroInspectorEntry  entry) => DrawNumber<float>(entry, ImGuiDataType.Float);
        private bool DrawDouble(AeroInspectorEntry entry) => DrawNumber<double>(entry, ImGuiDataType.Double);

        private bool DrawText(AeroInspectorEntry entry)
        {
            var val     = entry.GetValue<string>();
            var changed = ImGui.InputText($"###{entry.Name}", ref val, 1000);
            if (changed) entry.SetValue(val);

            return changed;
        }

        private bool DrawVector2(AeroInspectorEntry entry)
        {
            var val        = entry.GetValue<Vector2>();
            var hasChanged = ImTool.Widgets.Vector2(ref val, entry.Name);
            if (hasChanged) entry.SetValue(val);

            return hasChanged;
        }

        private bool DrawVector3(AeroInspectorEntry entry)
        {
            var val        = entry.GetValue<Vector3>();
            var hasChanged = ImTool.Widgets.Vector3(ref val, entry.Name);
            if (hasChanged) entry.SetValue(val);

            return hasChanged;
        }

        private bool DrawVector4(AeroInspectorEntry entry)
        {
            var val        = entry.GetValue<Vector4>();
            var hasChanged = ImTool.Widgets.Vector4(ref val, entry.Name);
            if (hasChanged) entry.SetValue(val);

            return hasChanged;
        }

        private bool DrawQuaternion(AeroInspectorEntry entry)
        {
            var val        = entry.GetValue<Quaternion>();
            var hasChanged = ImTool.Widgets.Quaternion(ref val, entry.Name);
            if (hasChanged) entry.SetValue(val);

            return hasChanged;
        }

        private bool DrawUnknown(AeroInspectorEntry entry)
        {
            ImGui.Text($"{entry.Ref.GetValue(entry.Obj)}");

            return false;
        }

        private AeroInspectorEntry.EntryType GetEntryTypeFromType(Type type)
        {
            var eType = Type.GetTypeCode(type) switch
            {
                TypeCode.Int32  => AeroInspectorEntry.EntryType.Int,
                TypeCode.Int64  => AeroInspectorEntry.EntryType.Long,
                TypeCode.Int16  => AeroInspectorEntry.EntryType.Short,
                TypeCode.UInt32 => AeroInspectorEntry.EntryType.Uint,
                TypeCode.UInt64 => AeroInspectorEntry.EntryType.Ulong,
                TypeCode.UInt16 => AeroInspectorEntry.EntryType.Ushort,
                TypeCode.Byte   => AeroInspectorEntry.EntryType.Byte,
                TypeCode.Char   => AeroInspectorEntry.EntryType.Char,
                TypeCode.Single => AeroInspectorEntry.EntryType.Float,
                TypeCode.Double => AeroInspectorEntry.EntryType.Double,
                TypeCode.String => AeroInspectorEntry.EntryType.String,
                _               => AeroInspectorEntry.EntryType.Unknown
            };

            if (eType == AeroInspectorEntry.EntryType.Unknown) {
                if (type.GetCustomAttribute<AeroBlockAttribute>() != null) {
                    return AeroInspectorEntry.EntryType.AeroBlock;
                }

                switch (type.ToString()) {
                    case "System.Numerics.Vector2":
                        return AeroInspectorEntry.EntryType.Vector2;
                    case "System.Numerics.Vector3":
                        return AeroInspectorEntry.EntryType.Vector3;
                    case "System.Numerics.Vector4":
                        return AeroInspectorEntry.EntryType.Vector4;
                    case "System.Numerics.Quaternion":
                        return AeroInspectorEntry.EntryType.Quaternion;
                }
            }

            return eType;
        }
    }

    public class AeroInspectorEntry
    {
        public string    Name;
        public EntryType EType;
        public bool      IsArray;
        public FieldInfo Ref;
        public int       ColorIdx;
        public int       Offset;
        public int       Size;
        public int       OrderIdx;
        public object    Obj;

        public AeroInspectorEntry       Parent;
        public List<AeroInspectorEntry> SubEntrys = new();

        public T GetValue<T>()
        {
            if (Parent is {IsArray: true}) {
                var arr = (Array) Obj;
                var idx = (OrderIdx - Parent.OrderIdx) - 1;
                return (T) arr.GetValue(idx);
            }

            var val = (T) Ref.GetValue(Obj);
            return val;
        }

        public void SetValue<T>(T val)
        {
            if (Parent is {IsArray: true}) {
                var arr = (Array) Obj;
                var idx = (OrderIdx - Parent.OrderIdx) - 1;
                arr.SetValue(val, idx);
                return;
            }

            Ref.SetValue(Obj, val);
        }

        public string GetFullName()
        {
            var names = new List<string>(5);

            var currentEntry = this;
            while (currentEntry != null) {
                names.Add(currentEntry.Name);
                currentEntry = currentEntry.Parent;
            }

            names.Reverse();
            var fullName = string.Join('.', names);
            return fullName;
        }

        public enum EntryType
        {
            Byte,
            Char,
            Int,
            Uint,
            Long,
            Ulong,
            Short,
            Ushort,
            Float,
            Double,
            String,
            Vector2,
            Vector3,
            Vector4,
            Quaternion,
            AeroBlock,
            Unknown
        }
    }
}