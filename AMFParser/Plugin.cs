using System;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using Fiddler;
using Viewers;

[assembly: Fiddler.RequiredVersion("2.2.2.0")]

public class ClassDefinition
{
    public string TypeIdentifier;
    public ArrayList ClassMemberDefinitions;
    public uint IsExternalizable;
    public uint IsDynamic;

    public ClassDefinition(
        string pTypeIdentifier,
        ArrayList pClassMemberDefinitions,
        uint pIsExternalizable,
        uint pIsDynamic
    )
    {
        TypeIdentifier = pTypeIdentifier;
        ClassMemberDefinitions = pClassMemberDefinitions;
        IsExternalizable = pIsExternalizable;
        IsDynamic = pIsDynamic;
    }

}

public struct TypeAndData
{
    public int Type;
    public string TypeStr;
    public Object Data;
}

public class AMF3Object
{
    public string TypeIdentifier ;
    public uint IsExternalizable ;
    public uint IsDynamic ;
    public uint ClassMemberCount;
    public uint IsInLine;
    public uint IsInLineClassDef;
    public ArrayList ClassMemberDefinitions;
    public ArrayList Parameters;
    public Dictionary<string, Object> DynamicMembers;

    public AMF3Object()
    {
        ClassMemberDefinitions = new ArrayList();
        Parameters=new ArrayList();
        DynamicMembers=new Dictionary<string, Object>();
    }
}

public class AMFDataParser
{
    private int Offset;
    private byte[] Data;
    string DebugStr ;
    int Level;
    int DebugLevel;
    ArrayList ClassDefinitions;
    TypeAndData AnalyzedData;
    TreeControl OneTreeControl;
    ArrayList StringRefs;

    public AMFDataParser()
    {
        DebugLevel = 0;
        Level = 0;
        Offset = 0;
        DebugStr = "";
    }

    public void ProcessData(byte[] pmData)
    {
        Level = 0;
        Data = pmData;
        Offset = 0;
        DebugStr = "";

        ClassDefinitions = new ArrayList();
        StringRefs = new ArrayList();
        int Version = ReadInt();
        int HeaderLength = ReadInt();
        DebugStr += String.Format("{0}: Version: {1:G}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, Version);
        DebugStr += String.Format("{0}: Header Length: {1:G}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, HeaderLength);
        //TODO: Parse Header
        for (int HeaderI = 0; HeaderI < HeaderLength; HeaderI++)
        {
            DebugStr += System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
            DumpHex(Offset, 20);
        }

        int BodyLength = ReadInt();
        DebugStr += String.Format("{0}: Body Length: {1:G}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, BodyLength);
        for (int BodyI = 0; BodyI < BodyLength; BodyI++)
        {
            string Target = ReadUTF();
            string Response = ReadUTF();
            int Reserved = ReadLong();
            DebugStr += String.Format("{0}: Target({1}) Response({2})\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, Target, Response);
            
            AnalyzedData = ReadData();

            /*
            try
            {
                DumpData(AnalyzedData, 0);
            }
            catch (Exception e)
            {
                DebugStr += "Exception: " + e.StackTrace;
            }*/            
        }
    }

    public void DrawOnTreeControl(TreeControl ParamOneTreeControl)
    {
        OneTreeControl=ParamOneTreeControl;
        OneTreeControl.treeView.Nodes.Clear();

        TreeNode RootNode=OneTreeControl.treeView.Nodes.Add("Root");
        DumpData(AnalyzedData, 0, RootNode);
        //OneTreeControl.treeView.ExpandAll();
        OneTreeControl.treeView.SelectedNode = RootNode;
    }

    void DumpData(Object AObject, int Level, TreeNode ParentNode)
    {
        TreeNode CurrentNode;
        try
        {
            string Prefix = new string(' ', Level);
            string DataType = AObject.GetType().Name ;
            if(DebugLevel > 2)
                DebugStr += Prefix + "["+AObject.GetType().Name+"]\r\n";
            if (DataType == "TypeAndData")
            {
                TypeAndData TypeAndDataEntry = (TypeAndData)AObject;
                if (DebugLevel > 3)
                    DebugStr += Prefix + "Type=" + TypeAndDataEntry.Type + "\r\n";
                CurrentNode = ParentNode;
                if (TypeAndDataEntry.Data != null)
                {
                    DumpData(TypeAndDataEntry.Data, Level + 1, CurrentNode);
                }
            }
            else if (DataType == "ArrayList")
            {
                ArrayList OneArrayList = (ArrayList)AObject;
                Dictionary<string, Object> KeyDictionary = (Dictionary<string,Object>)OneArrayList[0];
                ArrayList ValueArrayList = (ArrayList)OneArrayList[1];

                for (int i = 0; i < ValueArrayList.Count; i++)
                {
                    CurrentNode = ParentNode.Nodes.Add("");
                    DumpData(ValueArrayList[i], Level + 1, CurrentNode);
                }
            }
            else if (DataType == "Dictionary`2")
            {
                Dictionary<string, Object> ADic = (Dictionary<string, Object>)AObject;
                foreach(KeyValuePair<string,Object> entry in ADic)
                {
                    DebugStr+=Prefix+entry.Key+": \r\n";
                    CurrentNode = ParentNode.Nodes.Add(entry.Key);
                    DumpData(entry.Value, Level + 1, CurrentNode);
                }
            }
            else if (DataType == "Int32")
            {
                Int32 Value=(Int32)AObject;
                DebugStr += Prefix + Value + "\r\n";
                CurrentNode = ParentNode.Nodes.Add(Value.ToString());
            }
            else if (DataType == "UInt32")
            {
                UInt32 Value = (UInt32)AObject;
                DebugStr += Prefix + Value + "\r\n";
                CurrentNode = ParentNode.Nodes.Add(Value.ToString());
            }
            else if (DataType == "Double")
            {
                Double Value = (Double)AObject;
                DebugStr += Prefix + Value + "\r\n";
                CurrentNode = ParentNode.Nodes.Add(Value.ToString());
            }
            else if (DataType == "DateTime")
            {
                DateTime Value = (DateTime)AObject;
                DebugStr += Prefix + Value + "\r\n";
                CurrentNode = ParentNode.Nodes.Add(Value.ToString());
            }
            else if (DataType == "String")
            {
                String Value = (String)AObject;
                if(Value.Length>0)
                    DebugStr += Prefix + Value + "\r\n";
                CurrentNode = ParentNode.Nodes.Add(Value);
            }
            else if (DataType == "AMF3Object")
            {
                AMF3Object OneAMF3Object = (AMF3Object)AObject;
                DebugStr += Prefix + OneAMF3Object.TypeIdentifier + "\r\n"; ;

                if (ParentNode.Text == "" )
                {
                    ParentNode.Text = OneAMF3Object.TypeIdentifier;
                }
                else
                {
                    ParentNode.Text = ParentNode.Text + " - " + OneAMF3Object.TypeIdentifier; 
                }

                for (int i = 0; i < OneAMF3Object.Parameters.Count; i++)
                {
                    if (OneAMF3Object.ClassMemberDefinitions.Count > 0)
                    {
                        CurrentNode = ParentNode.Nodes.Add(OneAMF3Object.ClassMemberDefinitions[i].ToString());
                    }
                    else
                    {
                        //CurrentNode = ParentNode.Nodes.Add("");
                        CurrentNode = ParentNode;
                    }
                    DumpData(OneAMF3Object.Parameters[i], Level + 1, CurrentNode);
                }
                for (int i = 0; i < OneAMF3Object.DynamicMembers.Count; i++)
                {
                    foreach (KeyValuePair<string, Object> entry in OneAMF3Object.DynamicMembers)
                    {
                        DebugStr += Prefix + entry.Key + "\r\n";
                        CurrentNode = ParentNode.Nodes.Add(entry.Key);
                        DumpData(entry.Value, Level + 1, CurrentNode);
                    }
                }
            }
            else
            {
                DebugStr += Prefix + AObject.GetType().Name + "";
            }
        }
        catch (Exception e)
        {
            DebugStr += "Exception: " + e.StackTrace+"\r\n";
            DebugStr += AObject.ToString()+"\r\n";
        }
    }

    public void DumpHexRemaining()
    {
        string Prefix = new string(' ', Level);
        if (Offset < Data.Length)
        {
            DumpHex(Offset, Data.Length - Offset);
            DebugStr += "Remaining Bytes\r\n";
        }
    }

    public void DumpHex(int DumpOffset, int Length)
    {
        string Prefix = new string(' ', Level);
        if (Length == 0)
            Length = Data.Length - DumpOffset;
        string AsciiStr = "";

        DebugStr += Prefix;
        for (int i = 0; i < Length; i++)
        {
            DebugStr += String.Format("{0:x2} ", Data[DumpOffset + i]);
            string AsciiCh = ".";
            if (Data[DumpOffset + i] < 128 && Data[DumpOffset + i] > 10)
                AsciiCh = System.Convert.ToChar(Data[DumpOffset + i]).ToString();

            AsciiStr += AsciiCh;
            if (i % 16 == 15)
            {
                DebugStr += "   " + AsciiStr + "\r\n" + Prefix;
                AsciiStr = "";
            }
        }
        DebugStr += "\r\n";
    }

    public int ReadByte()
    {
        return Data[Offset++];
    }

    public int ReadInt()
    {
        return Data[Offset++] << 8 | Data[Offset++];
    }

    public int ReadLong()
    {
        return Data[Offset++] << 24 | Data[Offset++] << 16 | Data[Offset++] << 8 | Data[Offset++];
    }

    public double ReadDouble()
    {
        byte[] bytes = { Data[Offset+7], Data[Offset+6], Data[Offset+5], Data[Offset+4], Data[Offset+3], Data[Offset+2], Data[Offset+1], Data[Offset] };
        Offset = Offset + 8;
        Double value = BitConverter.ToDouble(bytes, 0);
        return value;
    }

    public string ReadBuffer(int Length)
    {
        Level++;

        string RetStr = "";
        int LastOffset = Offset;
        Offset += Length;

        for (int i = LastOffset; i < Offset; i++)
        {
            RetStr += System.Convert.ToChar(Data[i]).ToString();
        }
        Level--;
        return RetStr;
    }

    public string ReadUTF()
    {
        Level++;
        string Prefix = new string(' ', Level);
        int Length = ReadInt();
        if(DebugLevel>3)
            DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " Reading " + Length + "Bytes @ Offset "+Offset+"\r\n";
        string Ret = ReadBuffer(Length);
        Level--;
        return Ret;
    }

    public uint ReadAMF3Int()
    {
        Level++;
        string Prefix = new string(' ', Level);
        uint Byte = (uint)ReadByte();
        if (Byte < 128)
        {
            Level--;
            return Byte;
        }
        else
        {
            Byte = (Byte & 0x7f) << 7;
            uint NewByte = (uint)ReadByte();
            if (NewByte < 128)
            {
                Level--;
                return Byte | NewByte;
            }
            else
            {
                Byte = (Byte | (NewByte & 0x7f)) << 7;
                NewByte = (uint)ReadByte();
                if (NewByte < 128)
                {
                    Level--;
                    return Byte | NewByte;
                }
                else
                {
                    Byte = (Byte | (NewByte & 0x7f)) << 8;
                    NewByte = (uint)ReadByte();
                    Byte |= NewByte;
                    if ((Byte & 0x10000000) != 0)
                        Byte |= 0xe0000000;
                    Level--;
                    return Byte;
                }

            }
        }
    }

    public string ReadAMF3String()
    {
        Level++;
        string Prefix = new string(' ', Level);
        uint StrRef = ReadAMF3Int();
        string Str;

        if (DebugLevel > 4)
            DebugStr += String.Format("{0}{1}: StrRef: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, StrRef);
        if ((StrRef & 1) == 1)
        {
            uint StrLen = StrRef >> 1;

            Str = ReadBuffer((int)StrLen);
            if (DebugLevel > 4)
                DebugStr += Prefix + Str + "\r\n";
            Level--;

            if ( Str != null ) {
                StringRefs.Add(Str);
            }
        }
        else
        {
            uint j = StrRef >> 1;
            Str = (string)StringRefs[(int)j];
        }

        return Str;
        
    }

    public AMF3Object ReadAMF3Object()
    {
        AMF3Object ReturnValue=new AMF3Object();
        Level++;
        string Prefix = new string(' ', Level);

        uint ReferncePtr = ReadAMF3Int();
        if(DebugLevel>2)
            DebugStr += String.Format("{0}{1}: ReferncePtr: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReferncePtr);
        ReturnValue.TypeIdentifier = "";
        ReturnValue.IsExternalizable = 0;
        ReturnValue.IsDynamic = 0;
        ReturnValue.ClassMemberCount = 0;
        ReturnValue.IsInLine = (ReferncePtr & 1);
        ReferncePtr >>= 1;

        if (DebugLevel > 2)
            DebugStr += String.Format("{0}{1}: IsInLine: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsInLine);
        if (ReturnValue.IsInLine == 1)
        {
            ReturnValue.IsInLineClassDef = ReferncePtr & 1;
            ReferncePtr >>= 1;
            if (DebugLevel > 2)
                DebugStr += String.Format("{0}{1}: IsInLineClassDef: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsInLineClassDef);

            if (ReturnValue.IsInLineClassDef == 1)
            {
                ReturnValue.TypeIdentifier = ReadAMF3String();

                if (DebugLevel > 0)
                    DebugStr += Prefix + "TypeIdentifier:" + ReturnValue.TypeIdentifier + "\r\n";
                ReturnValue.IsExternalizable = ReferncePtr & 1;

                ReferncePtr >>= 1;
                if (DebugLevel > 2)
                    DebugStr += String.Format("{0}{1}: IsExternalizable: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsExternalizable);

                ReturnValue.IsDynamic = ReferncePtr & 1;

                ReferncePtr >>= 1;
                if (DebugLevel > 2)
                    DebugStr += String.Format("{0}{1}: IsDynamic: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsDynamic);

                ReturnValue.ClassMemberCount = ReferncePtr;
                if (DebugLevel > 2)
                    DebugStr += String.Format("{0}{1}: ClassMemberCount: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.ClassMemberCount);

                for (int i = 0; i < ReturnValue.ClassMemberCount; i++)
                {
                    string ClassMemberDefinition = ReadAMF3String();
                    ReturnValue.ClassMemberDefinitions.Add(ClassMemberDefinition);
                    if (DebugLevel > 2)
                        DebugStr += Prefix + "ClassMemberDefinition(" + i + "): " + ClassMemberDefinition + "\r\n";
                }


                ClassDefinitions.Add(
                    new ClassDefinition(
                        ReturnValue.TypeIdentifier,
                        ReturnValue.ClassMemberDefinitions,
                        ReturnValue.IsExternalizable,
                        ReturnValue.IsDynamic
                    )
                );
            }
            else
            {
                ClassDefinition OneClassDefinition = (ClassDefinition)ClassDefinitions[(int)ReferncePtr];
                ReturnValue.TypeIdentifier = OneClassDefinition.TypeIdentifier;
                ReturnValue.ClassMemberDefinitions = OneClassDefinition.ClassMemberDefinitions;
                ReturnValue.IsExternalizable = OneClassDefinition.IsExternalizable;
                ReturnValue.IsDynamic = OneClassDefinition.IsDynamic;
            }
        }
        else
        {
            DebugStr += Prefix + "TODO: Use ReferncePtr\r\n\r\n";
            return ReturnValue;
        }

        if (ReturnValue.IsExternalizable == 1)
        {
            switch (ReturnValue.TypeIdentifier)
            {
                case "flex.messaging.io.ArrayCollection":
                case "flex.messaging.io.ObjectProxy":
                    TypeAndData DataArray = ReadAMF3Data();
                    ReturnValue.Parameters.Add(DataArray);
                    break;
                case "DSK":
                    // skip two bytes
                    Offset++;
                    Offset++;
                    // Read the inner type and add it
                    TypeAndData StartType = ReadAMF3Data();
                    String StartClass = ((AMF3Object)StartType.Data).TypeIdentifier;
                    ReturnValue.ClassMemberDefinitions.Add(StartClass);
                    ReturnValue.Parameters.Add(StartType);
                    break;
                default:
                    DebugStr += Prefix + "Can't read " + ReturnValue.TypeIdentifier;
                    break;
            }
        }
        else
        {
            for (int i = 0; i < ReturnValue.ClassMemberDefinitions.Count; i++)
            {
                
                if (DebugLevel > 0)
                    DebugStr += Prefix + ReturnValue.ClassMemberDefinitions[i].ToString() + "\r\n";
                ReturnValue.Parameters.Add(ReadAMF3Data()); //Value
            }
            if (ReturnValue.IsDynamic == 1)
            {
                string Key = ReadAMF3String(); //Key
                while (Key.Length > 0)
                {
                    if (DebugLevel > 0)
                        DebugStr += Prefix + "Key=" + Key;
                    ReturnValue.DynamicMembers.Add(Key, ReadAMF3Data()); //Value                    
                    Key = ReadAMF3String(); //Key
                }
            }
        }

        Level--;
        return ReturnValue;
    }

    public ArrayList ReadAMF3Array()
    {
        Level++;

        string Prefix = new string(' ', Level);
        uint ReferncePtr = ReadAMF3Int();
        Dictionary<string, Object> KeyValues = new Dictionary<string, Object>();
        ArrayList DataList = new ArrayList();

        if (DebugLevel > 2)
            DebugStr += String.Format("{0}{1}: ReferncePtr: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReferncePtr);

        uint InLine = ReferncePtr & 1;
        ReferncePtr >>= 1;
        if (InLine != 0)
        {
            string Key = ReadAMF3String();
            while (Key.Length > 0)
            {
                KeyValues.Add(Key,ReadAMF3Data());
                if (DebugLevel > 2)
                    DebugStr += Key + "\r\n";                
                Key = ReadAMF3String();
            }

            for (int i = 0; i < ReferncePtr; i++)
            {
                DataList.Add(ReadAMF3Data()); //Value
            }
        }
        else
        {
            //TODO: Use ref
            DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
        }

        Level--;

        ArrayList ReturnValues=new ArrayList();
        ReturnValues.Add(KeyValues);
        ReturnValues.Add(DataList);
        return ReturnValues;
    }

    public DateTime ReadAMF3Date()
    {
        Level++;
        string Prefix = new string(' ', Level);

        uint ReferncePtr = ReadAMF3Int();
        uint InLine = ReferncePtr&0x1;
        ReferncePtr >>= 1;

        DateTime DateValue = new DateTime(1970, 1, 1);

        if (InLine == 1)
        {
            DateValue = DateValue.AddMilliseconds ( ReadDouble() );
        }
        else
        {
            //TODO: Referencing
        }
        Level--;
        return DateValue ;
    }

    public TypeAndData ReadAMF3Data()
    {
        Level++;
        string Prefix = new string(' ', Level);
        int Type = ReadByte();
        if (DebugLevel > 2)
            DebugStr += String.Format("{0}{1}: Type: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Type);

        TypeAndData ReturnValue=new TypeAndData();
        ReturnValue.Type = Type;
        ReturnValue.Data = null;

        switch (Type)
        {
            case 0x00:
                //undefined
                ReturnValue.TypeStr = "undefined";
                break;
            case 0x01:
                //null; 
                ReturnValue.TypeStr = "null";
                break;
            case 0x02:
                //false; //boolean false
                ReturnValue.TypeStr = "false";
                break;
            case 0x03:
                //true;  //boolean true
                ReturnValue.TypeStr = "true";
                break;
            case 0x04:
                //Amf3Int
                ReturnValue.TypeStr = "Amf3Int";
                ReturnValue.Data = ReadAMF3Int();
                if (DebugLevel > 2)
                    DebugStr += String.Format("{0}{1}: Value: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.Data);
                break;
            case 0x05:
                ReturnValue.TypeStr = "Double";
                ReturnValue.Data = ReadDouble();
                break;
            case 0x06:
                //Amf3String
                ReturnValue.TypeStr = "Amf3String";
                ReturnValue.Data = ReadAMF3String();
                if (DebugLevel > 0)
                    DebugStr += Prefix + ReturnValue.Data + "\r\n";
                break;
            case 0x07:
                ReturnValue.TypeStr = "Amf3XmlString";
                //TODO: Amf3XmlString
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3String\r\n";
                DumpHex(Offset, 20);
                break;
            case 0x08:
                //Amf3Date
                ReturnValue.TypeStr = "Amf3Date";
                ReturnValue.Data = ReadAMF3Date();
                break;
            case 0x09: //OK
                //Amf3Array
                ReturnValue.TypeStr = "Amf3Array";
                ReturnValue.Data = ReadAMF3Array();
                break;
            case 0x0A:
                //Amf3Object
                ReturnValue.TypeStr = "Amf3Object";
                ReturnValue.Data = ReadAMF3Object();
                break;
            case 0x0B:
                //TODO: Amf3XmlString
                ReturnValue.TypeStr = "Amf3XmlString";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3XmlString\r\n";
                DumpHex(Offset, 20);
                break;
            case 0x0C:
                //TODO: Amf3ByteArray
                ReturnValue.TypeStr = "Amf3ByteArray";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3ByteArray\r\n";
                DumpHex(Offset, 20);
                break;
        }

        Level--;
        return ReturnValue;
    }

    public ArrayList ReadArray()
    {
        Level++;
        string Prefix = new string(' ', Level);

        ArrayList ReturnValues = new ArrayList();
        int Length = ReadLong();
        DebugStr += String.Format("{0}{1}: Length: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Length);

        for (int i = 0; i < Length; i++)
        {
            ReturnValues.Add(ReadData());
        }
        Level--;
        return ReturnValues;
    }

    enum AMFDataTypes
    {
        Number = 0,
        Boolean,
        String,
        Object,
        Null,
        Undefined,
        Reference,
        MixedArray,
        Array = 10,
        Date,
        LongUTF,
        Internal,
        XML = 15,
        CustomClass,
        AMF3
    }

    public TypeAndData ReadData()
    {
        Level++;
        string Prefix = new string(' ', Level);
        int Type = ReadByte();
        TypeAndData ReturnValue = new TypeAndData();
        ReturnValue.Type = Type;
        ReturnValue.Data = null;

        if(DebugLevel > 2)
            DebugStr += String.Format("{0}{1}: Type: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Type);

        switch (Type)
        {
            case (int)AMFDataTypes.Number:
                ReturnValue.TypeStr = "Number";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.Boolean:
                ReturnValue.TypeStr = "Boolean";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.String:
                ReturnValue.TypeStr = "String";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.Object:
                ReturnValue.TypeStr = "Object";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.Null:
                ReturnValue.TypeStr = "Null";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.Undefined:
                ReturnValue.TypeStr = "Undefined";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.Reference:
                ReturnValue.TypeStr = "Reference";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.Array:
                ReturnValue.TypeStr = "Array";
                ReturnValue.Data = ReadArray();
                break;
            case (int)AMFDataTypes.Date:
                ReturnValue.TypeStr = "Date";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.LongUTF:
                ReturnValue.TypeStr = "LongUTF";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.Internal:
                ReturnValue.TypeStr = "Internal";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.XML:
                ReturnValue.TypeStr = "XML";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.CustomClass:
                ReturnValue.TypeStr = "CustomClass";
                DebugStr += Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: \r\n";
                DumpHex(Offset, 20);
                break;
            case (int)AMFDataTypes.AMF3:
                ReturnValue.TypeStr = "AMF3";
                ReturnValue.Data =  ReadAMF3Data();
                break;
        }
        Level--;
        return ReturnValue;
    }

    public int GetCurrentOffset()
    {
        return Offset;
    }

    public string GetDebugStr()
    {
        return DebugStr;
    }
}

public class AMFRequestInspector : Inspector2, IRequestInspector2
{
    private byte[] _body;
    private bool m_bDirty;
    private bool m_bReadOnly;
    private TreeControl oAMFTree;
    private TXTEditor oAMFEditor = new TXTEditor();
    bool IsAMFContent;
    AMFDataParser m_AMFData;

    public AMFRequestInspector()
    {
        IsAMFContent = false;
        m_AMFData = new AMFDataParser();
    }

    public bool bReadOnly
    {
        get
        {
            return m_bReadOnly;
        }
        set
        {
            m_bReadOnly = value;
        }
    }

    public bool bDirty
    {
        get
        {
            return m_bDirty;
        }
    }

    public override int GetOrder()
    {
        return 0;
    }

    public void Clear()
    {
        IsAMFContent = false;
        m_bDirty = false;
        oAMFEditor.Data = null;
    }

    public override void AddToTab(TabPage tabPage)
    {
        oAMFTree = new TreeControl();
        tabPage.Text = "AMF";
        tabPage.Controls.Add(oAMFTree);
        oAMFTree.Dock = DockStyle.Fill;

    }

    public HTTPRequestHeaders headers
    {
        get
        {
            return null;    // Return null if your control doesn't allow header editing.
        }
        set
        {
            if (value.ExistsAndEquals("content-type", "application/x-amf"))
                IsAMFContent = true;
            else
                IsAMFContent = false;
        }
    }

    public byte[] body
    {
        get
        {
            return _body;
        }
        set
        {
            _body = value;
            if (IsAMFContent)
            {
                try
                {
                    m_AMFData.ProcessData(_body);
                    m_AMFData.DrawOnTreeControl(oAMFTree);
                    m_AMFData.DumpHexRemaining();
                    oAMFEditor.Data += m_AMFData.GetDebugStr();
                }
                catch (Exception e)
                {
                    m_AMFData.DumpHexRemaining();
                    oAMFEditor.Data += m_AMFData.GetDebugStr();
                    oAMFEditor.Data += "Exception: " + e.StackTrace;
                }
            }
        }
    }
}

public class AMFResponseInspector : Inspector2, IResponseInspector2
{
    HTTPResponseHeaders _headers;
    private byte[] _body;
    private bool m_bDirty;
    private bool m_bReadOnly;
    private TreeControl oAMFTree;
    private TXTEditor oAMFEditor = new TXTEditor();
    bool IsAMFContent;
    AMFDataParser m_AMFData;

    public AMFResponseInspector()
    {
        m_AMFData = new AMFDataParser();
        IsAMFContent = false;
    }

    public bool bReadOnly
    {
        get
        {
            return m_bReadOnly;
        }
        set
        {
            m_bReadOnly = value;
        }
    }

    public bool bDirty
    {
        get
        {
            return m_bDirty;
        }
    }
    public override int GetOrder()
    {
        return 0;
    }

    public void Clear()
    {
        IsAMFContent = false;
        m_bDirty = false;
        oAMFEditor.Data = null;
    }
    public override void AddToTab(TabPage tabPage)
    {
        oAMFTree = new TreeControl();
        tabPage.Text = "AMF";
        tabPage.Controls.Add(oAMFTree);
        oAMFTree.Dock = DockStyle.Fill;
    }

    public HTTPResponseHeaders headers
    {
        get
        {
            return null;
        }
        set
        {
            _headers = value;
            if (value.ExistsAndEquals("content-type", "application/x-amf"))
                IsAMFContent = true;
            else
                IsAMFContent = false;

        }
    }

    public byte[] body
    {
        get
        {
            return _body;
        }
        set
        {
            _body = value;
            if (IsAMFContent)
            {
                try
                {
                    m_AMFData.ProcessData(_body);
                    oAMFTree.treeView.BeginUpdate();
                    m_AMFData.DrawOnTreeControl(oAMFTree);
                    oAMFTree.treeView.EndUpdate();
                    //m_AMFData.DumpHexRemaining();
                    //oAMFEditor.Data += m_AMFData.GetDebugStr();
                }
                catch (Exception e)
                {
                    m_AMFData.DumpHexRemaining();
                    oAMFEditor.Data += m_AMFData.GetDebugStr();
                    oAMFEditor.Data += "Exception: " + e.StackTrace;
                }
            }
        }
    }
}

