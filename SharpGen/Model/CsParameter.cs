﻿// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Text;
using SharpGen.Config;
using SharpGen.CppModel;
using System.Reflection;

namespace SharpGen.Model
{
    public class CsParameter : CsMarshalBase
    {
        private bool _isFast;

        public CsParameter()
        {
            // Default is In
            Attribute = CsParameterAttribute.In;
            _isFast = false;
        }

        public CsParameterAttribute Attribute { get; set; }

        public bool HasParams { get; set; }

        public bool IsOptional { get; set; }

        public bool IsUsedAsReturnType { get; set; }

        public bool IsFastOut
        {
            get { return _isFast && IsOut; }
        }

        public string DefaultValue { get; set; }

        private const int SizeOfLimit = 16;

        protected override void UpdateFromTag(MappingRule tag)
        {
            base.UpdateFromTag(tag);
            if (tag.ParameterUsedAsReturnType.HasValue)
                IsUsedAsReturnType = tag.ParameterUsedAsReturnType.Value;
            if (tag.ParameterAttribute.HasValue && (tag.ParameterAttribute.Value & ParamAttribute.Fast) != 0)
                _isFast = true;

            DefaultValue = tag.DefaultValue;
        }

        public CppParameter CppParameter { get { return (CppParameter) CppElement; } }

        public bool IsFixed
        {
            get
            {
                if (Attribute == CsParameterAttribute.Ref || Attribute == CsParameterAttribute.RefIn)
                {
                    if (IsRefInValueTypeOptional || IsRefInValueTypeByValue)
                        return false;
                    return true;
                }
                if (Attribute == CsParameterAttribute.Out && !IsBoolToInt)
                    return true;
                if (IsArray && !IsComArray)
                    return true;
                return false;
            }
        }

        public string TempName
        {
            get { return Name + "_"; }
        }

        public bool IsRef
        {
            get { return Attribute == CsParameterAttribute.Ref; }
        }

        public bool IsComArray
        {
            get
            {
                return PublicType is CsComArray;
            }
        }

        public bool IsInComArrayLike
        {
            get
            {
                return IsArray && IsComObject && !IsOut;
            }
        }

        public bool IsComObject
        {
            get
            {
                return PublicType.GetType() == typeof(CsInterface);
            }
        }

        public bool IsRefIn
        {
            get { return Attribute == CsParameterAttribute.RefIn; }
        }

        public bool IsIn
        {
            get { return Attribute == CsParameterAttribute.In; }
        }

        public bool IsOut
        {
            get { return Attribute == CsParameterAttribute.Out; }
        }

        public bool IsPrimitive
        {
            get { return PublicType.Type != null && PublicType.Type.GetTypeInfo().IsPrimitive; }
        }

        public bool IsString
        {
            get { return PublicType.Type == typeof (string); }
        }

        public bool IsValueType
        {
            get { return PublicType is CsStruct || PublicType is CsEnum ||
                    (PublicType.Type != null && (PublicType.Type.GetTypeInfo().IsValueType || PublicType.Type.GetTypeInfo().IsPrimitive)); }
        }

        public bool IsStructClass
        {
            get { return PublicType is CsStruct && ((CsStruct)PublicType).GenerateAsClass; }
        }

        public bool HasNativeValueType
        {
            get { return (PublicType is CsStruct && (PublicType as CsStruct).HasMarshalType) ; }
        }

        public bool IsStaticMarshal
        {
            get { return (PublicType is CsStruct && (PublicType as CsStruct).IsStaticMarshal); }
        }

        public bool IsRefInValueTypeOptional
        {
            get { return IsRefIn && IsValueType && !IsArray && IsOptional; }
        }

        public bool IsRefInValueTypeByValue
        {
            get
            {
                return IsRefIn && IsValueType && !IsArray
                       && ((PublicType.SizeOf <= SizeOfLimit && !HasNativeValueType) || (CppParameter.Attribute & ParamAttribute.Value) != 0);
            }
        }

        public string ParamName
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                if (!IsFastOut && Attribute == CsParameterAttribute.Out && (!IsArray || PublicType.Name == "string"))
                    builder.Append("out ");
                else if ((Attribute == CsParameterAttribute.Ref || Attribute == CsParameterAttribute.RefIn) && !IsArray)
                {
                    if (!(IsRefInValueTypeOptional || IsRefInValueTypeByValue) && !IsStructClass)
                        builder.Append("ref ");
                } 
                else if (HasParams && IsArray)
                {
                    builder.Append("params ");
                }

                if (IsRefIn && IsValueType && !IsArray && IsOptional && !IsStructClass)
                    builder.Append(PublicType.QualifiedName + "?");
                else
                    builder.Append(PublicType.QualifiedName);

                if (IsArray && PublicType.Name != "string" && !IsComArray)
                    builder.Append("[]");
                builder.Append(" ");
                builder.Append(Name);
                return builder.ToString();
            }
        }

        // Example Code-gen:
        //
        //IntPtr pDevice_;
        //SharpDX.Interop.CalliVoid(_nativePointer, 3 * 4, &pDevice_);
        //pDevice = new SharpDX.Direct3D11.Device(pDevice_);

        //for (int i = 0; i < pSamplers.Length; i++)
        //    pSamplers[i] = new SamplerState(pSamplers_[i]);

        public string CallName
        {
            get
            {
                // All ComArray are handle the same way
                if (IsComArray)
                    return $"(void*)({Name}?.NativePointer ?? IntPtr.Zero)";

                if (IsOut)
                {
                    if (PublicType is CsInterface)
                    {
                        if (IsArray)
                        {
                            return IsOptional ? Name + "==null?(void*)0:" + TempName : TempName;
                        }
                        return "&" + TempName;
                    }
                    if (IsArray)
                    {
                        return IsComArray ? Name : TempName;
                    }
                    if (IsFixed && !HasNativeValueType)
                    {
                        return IsUsedAsReturnType ? "&" + Name : TempName;
                    }
                    if (HasNativeValueType || IsBoolToInt)
                    {
                        return "&" + TempName;
                    }

                    return IsValueType ? "&" + Name : TempName;
                }
                if (IsRefInValueTypeOptional)
                {
                    if (IsStructClass)
                    {
                        return "(" + Name + " != null)?&" + TempName + ":(void*)IntPtr.Zero";
                    }
                    return "(" + Name + ".HasValue)?&" + TempName + ":(void*)IntPtr.Zero";
                }
                if(IsRefInValueTypeByValue)
                {
                    return HasNativeValueType ? "&" + TempName : "&" + Name;
                }
                if (PublicType.QualifiedName == Global.GetGlobalName("Color4") && MarshalType.Type == typeof(int))
                {
                    return Name + ".ToArgb()";
                }
                if (!IsFixed && PublicType is CsEnum && !IsArray)
                    return "unchecked((int)" + Name + ")";
                if (PublicType.Type == typeof (string))
                    return "(void*)" + TempName;
                if (PublicType is CsInterface && Attribute == CsParameterAttribute.In && !IsArray)
                    return $"(void*)({Name}?.NativePointer ?? IntPtr.Zero)";
                if (IsArray)
                {
                    if (HasNativeValueType || IsBoolToInt)
                    {
                        return TempName;
                    }
                    if (IsValueType && IsOptional)
                    {
                        return TempName;
                    }
                }
                if (IsBoolToInt)
                    return "(" + Name + "?1:0)";
                if (IsFixed && !HasNativeValueType)
                    return TempName;
                if (PublicType.Type == typeof (IntPtr) && !IsArray)
                    return "(void*)" + Name;
                if (HasNativeValueType)
                {
                    return IsIn ? TempName : "&" + TempName;
                }
                if (PublicType.Name == Global.GetGlobalName("PointerSize"))
                    return "(void*)" + Name;
                return Name;
            }
        }

        public override object Clone()
        {
            var parameter = (CsParameter)base.Clone();
            parameter.Parent = null;
            return parameter;
        }
    }
}