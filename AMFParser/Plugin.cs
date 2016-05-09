#undef DEBUG_MESSAGE_GENERATION

using System;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using Fiddler;
using Viewers;

[assembly: Fiddler.RequiredVersion("2.2.2.0")]


struct NodeData
{
    public int Sequence;
    public object Child;
};

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

public class MetaObject
{
    static int CurrentSeq = 0;
    public MetaObject()
    {
        Seq = CurrentSeq;
        CurrentSeq++;
    }

    public int Seq;
    public int Type;
    public int Offset;

    public string Name;
    public string TypeStr;
    public Object Data;
}

public class AMF3Object
{
    public string TypeIdentifier;
    public uint IsExternalizable;
    public uint IsDynamic;
    public uint ClassMemberCount;
    public uint IsInLine;
    public uint IsInLineClassDef;
    public ArrayList ClassMemberDefinitions;
    public ArrayList Parameters;
    public Dictionary<string, object> DynamicMembers;

    public AMF3Object()
    {
        ClassMemberDefinitions = new ArrayList();
        Parameters = new ArrayList();
        DynamicMembers = new Dictionary<string, object>();
    }
}

enum AMFDataTypes
{
    Number = 0,
    Boolean,
    String,
    Object,
    MovieClip,
    Null,
    Undefined,
    Reference,
    EcmaArray,
    ObjectEnd,
    StrictArray, //0xA
    Date,
    LongUTF,
    Unsupported,
    RecordSet,
    XMLDocument,
    TypedObject, //0x10
    AMF3         //0x11
};

public class AMFDataParser
{
    private int Offset;

    private List<byte> Data = null;

    public int GetCurrentOffset()
    {
        return Offset;
    }

    public string GetDebugStr()
    {
        return DebugStr;
    }

    public byte[] DataBytes
    {
        get
        {
            byte[] bytes = new byte[Data.Count];
            for (int i = 0; i < Data.Count; i++)
            {
                bytes[i] = Data[i];
            }

            return bytes;
        }

        set
        {
            if (Data != null)
            {
                Data = null;
            }

            Data = new List<byte>();
            for (int i = 0; i < value.Length; i++)
            {
                Data.Add(value[i]);
            }
            Offset = 0;

            NewClassDefinitions.Clear();
            NewStringRefs.Clear();
        }
    }

    void AdjustOffset(int NewOffset = -1)
    {
        if (NewOffset == -1)
        {
            Offset = Data.Count;
        }
        else
        {
            Offset = NewOffset;
        }
    }

    string DebugStr;
    int Level;

    int _DebugLevel = 100;
    public int DebugLevel
    {
        get
        {
            return _DebugLevel;
        }
        set
        {
            _DebugLevel = value;
        }
    }

    string Prefix;
    void Enter()
    {
        Level++;
        Prefix = new string(' ', Level);
    }

    void Exit()
    {
        Level--;
        Prefix = new string(' ', Level);
    }

    public ArrayList ParsedArray;

    ArrayList ClassDefinitions;
    ArrayList NewClassDefinitions;

    ArrayList StringRefs = null;
    ArrayList NewStringRefs = null;

    public AMFDataParser()
    {
        Level = 0;
        Offset = 0;
        DebugStr = "";

        ParsedArray = new ArrayList();
        ClassDefinitions = new ArrayList();
        NewClassDefinitions = new ArrayList();
        StringRefs = new ArrayList();
        NewStringRefs = new ArrayList();
    }

    public void AddDebugMessage(string format, params Object[] args)
    {
#if DEBUG
        if (args != null && args.Length > 0)
            DebugStr += String.Format(format, args);
        else
            DebugStr += format;
#endif
    }

    public string GetDebugMessage()
    {
        return DebugStr;
    }

    private void AddMetaObject(ArrayList Array, string Name, Object NewObject)
    {
        MetaObject NewMetaObject = new MetaObject();
        NewMetaObject.Name = Name;
        NewMetaObject.TypeStr = null;
        NewMetaObject.Data = NewObject;
        NewMetaObject.Offset = Offset;
        Array.Add(NewMetaObject);
    }

    object GetMetaObjectData(object Element)
    {
        return ((MetaObject)Element).Data;
    }

    public void WriteAMFPacket()
    {
        byte[] pmNullData = {};
        DataBytes = pmNullData;

        WriteAMFHeader();
        WriteAMFMessage();
    }

    public int Version;
    public int HeaderCount;

    public bool ReadAMFHeader()
    {
        Version = ReadU16();
        HeaderCount = ReadU16();

        if (Version == 0x3 || Version == 0x00) //We support AMF0 or AMF3
        {
            if (_DebugLevel > 2)
            {
                AddDebugMessage("{0}: Version: {1:G}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, Version);
                AddDebugMessage(" Header Count: {1:G}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, HeaderCount);
            }

            ArrayList HeaderArray = new ArrayList();
            for (int i = 0; i < HeaderCount; i++)
            {
                AddMetaObject(HeaderArray, "Header Name", ReadAMF0String());

                if (_DebugLevel > 2)
                {
                    AddDebugMessage(" Skipping: 0x{1:x}\r\n",
                            System.Reflection.MethodBase.GetCurrentMethod().Name,
                            Data[Offset]
                           );
                }

                AddMetaObject(HeaderArray, "Must Understand", ReadByte());

                int HeaderLength = ReadLong();

                if (_DebugLevel > 2)
                {
                    AddDebugMessage(" HeaderLength: 0x{1:x}@0x{2:x}\r\n",
                            System.Reflection.MethodBase.GetCurrentMethod().Name,
                            HeaderLength,
                            Offset
                           );
                }
                AddMetaObject(HeaderArray, "Data", ReadAMF0());
            }

            AddMetaObject(ParsedArray, "Header", HeaderArray);

            if (_DebugLevel > 2)
            {
                AddDebugMessage(" Header End Offset: 0x{1:x}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, Offset);
            }
            return true;
        }

        return false;
    }

    public bool WriteAMFHeader()
    {
        WriteU16(Version);
        WriteU16(HeaderCount);

        if (Version == 0x3 || Version == 0x00)
        {
            ArrayList HeaderArray = (ArrayList)GetMetaObjectData(ParsedArray[0]);

             int j = 0;
            for (int i = 0; i < HeaderCount; i++)
            {
                WriteAMF0String((string) GetMetaObjectData(HeaderArray[j++]));
                WriteByte((byte)GetMetaObjectData(HeaderArray[j++]));

                //Reserve 4 bytes
                int StartOffset = Offset;
                WriteAMF0((MetaObject) GetMetaObjectData(HeaderArray[j++]));
                int HeaderLength = Offset - StartOffset;

                AdjustOffset(StartOffset);
                WriteLong(HeaderLength);
                AdjustOffset();
            }
            return true;
        }

        return false;
    }

    ArrayList MessageArray;

    public bool ReadAMFMessage()
    {
        //Message
        AddDebugMessage("{0}: Message Start @0x{1:x}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, Offset);
        int MessageCount = ReadU16();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}: MessageCount: {1:G}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, MessageCount);

        MessageArray = new ArrayList();
        for (int i = 0; i < MessageCount; i++)
        {
            string Target = ReadAMF0String();
            string Response = ReadAMF0String();
            int Reserved = ReadLong();

            AddMetaObject(MessageArray, "Target", Target);
            AddMetaObject(MessageArray, "Response", Response);
            AddMetaObject(MessageArray, "Message Length", Reserved);

            if (_DebugLevel > 2)
            {
                AddDebugMessage("{0}: Target: {1} Response: {2} Reserved:0x{3:x}\r\n", System.Reflection.MethodBase.GetCurrentMethod().Name, Target, Response, Reserved);
            }

            AddMetaObject(MessageArray, "Data", ReadAMF0());
        }

        AddMetaObject(ParsedArray, "Message", MessageArray);

        return true;
    }

    public bool WriteAMFMessage()
    {
        //Message
        //ArrayList MessageArray = (ArrayList)GetMetaObjectData(ParsedArray[Message]);
        WriteU16( MessageArray.Count/4 );

        for (int i = 0; i < MessageArray.Count; i+=4)
        {

            WriteAMF0String((string)GetMetaObjectData(MessageArray[i]));
            WriteAMF0String((string)GetMetaObjectData(MessageArray[i+1]));

            //Reserve 4 bytes
            int StartOffset = Offset;
            WriteAMF0((MetaObject)GetMetaObjectData(MessageArray[i+3]));
            int MessageLength = Offset - StartOffset;

            AdjustOffset(StartOffset);

            if ((int)GetMetaObjectData(MessageArray[i + 2]) == -1)
            {
                WriteLong((int)GetMetaObjectData(MessageArray[i + 2]));
            }
            else
            {
                WriteLong(MessageLength);
            }
            
            AdjustOffset();
        }
        return true;
    }

    public bool ReadAMFPacket(byte[] pmData)
    {
        ParsedArray.Clear();
        ClassDefinitions.Clear();
        NewClassDefinitions.Clear();
        StringRefs.Clear();
        NewStringRefs.Clear();
        NodeUpdateMap.Clear();

        Level = 0;

        Data = new List<byte>();
        for (int i = 0; i < pmData.Length; i++)
        {
            Data.Add(pmData[i]);
        }
        Offset = 0;
        DebugStr = "";

        if( ReadAMFHeader() )
        {
            if (ReadAMFMessage())
            {
                return true;
            }
        }

        return false;
    }

    TreeView DisplayTreeView;

    public void DrawOnTreeView(TreeView AMFTreeView = null)
    {
        if (AMFTreeView != null)
        {
            DisplayTreeView = AMFTreeView;
        }

        if (DisplayTreeView != null)
        {
            DisplayTreeView.Nodes.Clear();
            TreeNode RootNode = DisplayTreeView.Nodes.Add("Root");

            object ChildNewData;

            EnumerateNodes(ParsedArray, out ChildNewData, RootNode);

            DisplayTreeView.ExpandAll();
            DisplayTreeView.SelectedNode = RootNode;
            DisplayTreeView.Select();
        }
    }

    Dictionary<TreeNode, NodeData> NodeObjectMap = new Dictionary<TreeNode, NodeData>();

    public string GetTreeNodeData(TreeNode Node)
    {
        if (NodeObjectMap.ContainsKey(Node))
        {
            return NodeObjectMap[Node].Child.ToString();
        }
        return null;
    }

    public bool ReadOnly = true;
    public bool Dirty = false;

    Dictionary<int, string> NodeUpdateMap = new Dictionary<int, string>();
    public void SetTreeNodeData(TreeNode Node, string text)
    {
        if (NodeObjectMap.ContainsKey(Node))
        {
            object LinkedObject =NodeObjectMap[Node].Child;

#if DEBUG_MESSAGE_GENERATION
            MessageBox.Show(String.Format("Updated: {0} -> {1} Type: {2}",
                        NodeObjectMap[Node].Child.ToString(),
                        text,
                        NodeObjectMap[Node].Child.GetType()
                       ));
#endif

            NodeUpdateMap[NodeObjectMap[Node].Sequence] = text;

            object ChildNewData;

            EnumerateNodes( ParsedArray, out ChildNewData );

            Dirty = true;

            DrawOnTreeView();
        }
    }

    public int NodeSequence = 0;

    object AddNode(TreeNode ParentNode, out TreeNode NewNode, object child, string TypeStr = "")
    {
        if (ParentNode != null)
        {
            string text;

            text = child.ToString();

            NewNode = ParentNode.Nodes.Add(text);
            NodeData CurrentNodeData = new NodeData();
            CurrentNodeData.Sequence = NodeSequence;
            CurrentNodeData.Child = child;

            NodeObjectMap.Add(NewNode, CurrentNodeData);
        }
        else
        {
            NewNode = null;
            if (NodeUpdateMap.ContainsKey(NodeSequence))
            {
                NodeSequence++;

                switch (TypeStr)
                {
                    case "Int32":
                        return Convert.ToInt32(NodeUpdateMap[NodeSequence - 1]);
                    case "UInt32":
                        return Convert.ToUInt32(NodeUpdateMap[NodeSequence - 1]);
                    case "Double":
                        return Convert.ToDouble(NodeUpdateMap[NodeSequence - 1]);
                    case "DateTime":
                        return Convert.ToDateTime(NodeUpdateMap[NodeSequence - 1]);
                    case "String":
                        return NodeUpdateMap[NodeSequence - 1];
                }
            }
        }

        NodeSequence++;

        return null;
    }

    public string EnumerateNodes(
            object CurrentObject,
            out object NewData,
            TreeNode ParentNode = null,
            int Level = 0,
            string TypeStr = ""
        )
    {
        if( Level == 0 )
            NodeSequence = 0;

        Enter();

        NewData = null;

        TreeNode CurrentNode = null;
        string enumerate_str = "";
        object ChildNewData = null;

        try
        {
            string DataType = CurrentObject.GetType().Name;

            if (DataType == "MetaObject")
            {
                MetaObject MetaObjectEntry = (MetaObject)CurrentObject;

                enumerate_str += String.Format("{0} Name:[{1}] {2}({3}) @0x{4:x}\r\n",
                                Prefix,
                                MetaObjectEntry.Name,
                                MetaObjectEntry.Type,
                                MetaObjectEntry.TypeStr,
                                MetaObjectEntry.Offset);


                if (MetaObjectEntry.Data != null)
                {
                    CurrentNode = ParentNode;
                    if (MetaObjectEntry.Name != null && ParentNode != null)
                    {
                        CurrentNode = ParentNode.Nodes.Add("[" + MetaObjectEntry.Name + "]");
                    }

                    enumerate_str += EnumerateNodes(MetaObjectEntry.Data, out ChildNewData, CurrentNode, Level + 1, MetaObjectEntry.TypeStr == null ? "" : MetaObjectEntry.TypeStr);

                    if (ChildNewData != null)
                    {
                       MetaObjectEntry.Data = ChildNewData;
                    }
                }
            }
            else
            {
                enumerate_str += String.Format( "{0} Object Type: {1}\r\n", Prefix, DataType );
                if (DataType == "ArrayList")
                {
                    foreach (object sub_entry in (System.Collections.ArrayList)CurrentObject)
                    {
                        enumerate_str += EnumerateNodes(sub_entry, out ChildNewData, ParentNode, Level + 1);
                    }
                }
                else if (DataType == "Dictionary`2")
                {
                    Dictionary<string, object> ADic = (Dictionary<string, object>)CurrentObject;
                    foreach (KeyValuePair<string, object> entry in ADic)
                    {
                        enumerate_str += String.Format("{0} + entry.Key: {1}\r\n", Prefix, entry.Key);

                        AddNode(ParentNode, out CurrentNode, entry.Key);
 
                        enumerate_str += EnumerateNodes(entry.Value, out ChildNewData, CurrentNode, Level + 1);
                    }
                }
                else if (DataType == "AMF3Object")
                {
                    AMF3Object CurrentAMF3Object = (AMF3Object)CurrentObject;

                    enumerate_str += String.Format("{0} AMF3Object.TypeIdentifier: {1}\r\n", Prefix, CurrentAMF3Object.TypeIdentifier);

                    if (ParentNode != null)
                    {
                        if (ParentNode.Text == "")
                        {
                            ParentNode.Text = CurrentAMF3Object.TypeIdentifier;
                        }
                        else
                        {
                            ParentNode.Text = ParentNode.Text + " - " + CurrentAMF3Object.TypeIdentifier;
                        }
                    }

                    if (_DebugLevel > 2)
                        AddDebugMessage(Prefix + "Parameters.Count:" + CurrentAMF3Object.Parameters.Count + "\r\n");

                    for (int i = 0; i < CurrentAMF3Object.Parameters.Count; i++)
                    {
                        if (CurrentAMF3Object.ClassMemberDefinitions.Count > 0)
                        {
                            AddNode(ParentNode, out CurrentNode, CurrentAMF3Object.ClassMemberDefinitions[i], TypeStr);
                        }
                        else
                        {
                            //CurrentNode = ParentNode.Nodes.Add("");
                            CurrentNode = ParentNode;
                        }
                        enumerate_str += EnumerateNodes(CurrentAMF3Object.Parameters[i], out ChildNewData, CurrentNode, Level + 1);
                    }

                    if (_DebugLevel > 2)
                        AddDebugMessage(Prefix + "DynamicMembers.Count:" + CurrentAMF3Object.DynamicMembers.Count + "\r\n");

                    foreach (KeyValuePair<string, Object> entry in CurrentAMF3Object.DynamicMembers)
                    {
                        enumerate_str += String.Format("{0} Key: {1}\r\n", Prefix, entry.Key );

                        AddNode(ParentNode, out CurrentNode, entry.Key, TypeStr);
                        enumerate_str += EnumerateNodes(entry.Value, out ChildNewData, CurrentNode, Level + 1);
                    }

                    if (_DebugLevel > 2)
                        AddDebugMessage(Prefix + "DynamicMembers Enumeration Finished!\r\n");
                }
                else if (DataType == "Int32" || DataType == "UInt32" || DataType == "Double" || DataType == "DateTime" || DataType == "String")
                {
                    NewData = AddNode(ParentNode, out CurrentNode, CurrentObject, TypeStr);
                }
                else
                {
                    enumerate_str += String.Format("{0} Unrecognized Type: {1}\r\n", Prefix, CurrentObject.GetType().Name);
                }
            }
        }
        catch (Exception e)
        {
            if (_DebugLevel >= 0)
            {
                AddDebugMessage("Exception: " + e.StackTrace + "\r\n");
                AddDebugMessage(CurrentObject.ToString() + "\r\n");
            }
        }

        Exit();
        return enumerate_str;
    }

    public void DumpHexRemaining()
    {
        if (Offset < Data.Count)
        {
            DumpHex(Offset, Data.Count - Offset);
            AddDebugMessage("Remaining Bytes\r\n");
        }
    }

    public void DumpHex(int DumpOffset, int Length)
    {
        if (Length == 0)
            Length = Data.Count - DumpOffset;
        string AsciiStr = "";

        AddDebugMessage(Prefix);
        for (int i = 0; i < Length && i < Data.Count; i++)
        {
            AddDebugMessage("{0:x2} ", Data[DumpOffset + i]);
            string AsciiCh = ".";
            if (Data[DumpOffset + i] < 128 && Data[DumpOffset + i] > 10)
                AsciiCh = System.Convert.ToChar(Data[DumpOffset + i]).ToString();

            AsciiStr += AsciiCh;
            if (i % 16 == 15)
            {
                AddDebugMessage("   {0}\r\n" + Prefix, AsciiStr);
                AsciiStr = "";
            }
        }

        AddDebugMessage("   {0}\r\n" + Prefix, AsciiStr);
        AddDebugMessage("\r\n");
    }

    public byte ReadByte()
    {
        return Data[Offset++];
    }

    public void WriteByte(byte value)
    {
        Data.Insert(Offset++, value);
    }

    public int ReadU16()
    {
        return Data[Offset++] << 8 | Data[Offset++];
    }

    public void WriteU16(int Length)
    {
        Data.Insert(Offset++,(byte)((Length << 8) & 0xff));
        Data.Insert(Offset++,(byte)((Length & 0xff)));
    }

    public int ReadLong()
    {
        return Data[Offset++] << 24 | Data[Offset++] << 16 | Data[Offset++] << 8 | Data[Offset++];
    }

    public void WriteLong(int value)
    {
        Data.Insert(Offset++, (byte)((value >> 24) & 0xff));
        Data.Insert(Offset++, (byte)((value >> 16) & 0xff));
        Data.Insert(Offset++, (byte)((value >> 8) & 0xff));
        Data.Insert(Offset++, (byte)(value & 0xff));
    }

    public double ReadDouble()
    {
        byte[] bytes = { Data[Offset + 7], Data[Offset + 6], Data[Offset + 5], Data[Offset + 4], Data[Offset + 3], Data[Offset + 2], Data[Offset + 1], Data[Offset] };

        Double value = BitConverter.ToDouble(bytes, 0);

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: value: {2:F} @0x{3:x}\r\n{0} 0x{4:x} 0x{5:x} 0x{6:x} 0x{7:x}\r\n{0} 0x{8:x} 0x{9:x} 0x{10:x} 0x{11:x}\r\n",
                new string(' ', Level),
                System.Reflection.MethodBase.GetCurrentMethod().Name,
                value,
                Offset - 8,
                Data[Offset], Data[Offset + 1], Data[Offset + 2], Data[Offset + 3],
                Data[Offset + 4], Data[Offset + 5], Data[Offset + 6], Data[Offset + 7]
                );

        Offset = Offset + 8;

        return value;
    }

    public void WriteDouble(double value)
    {
        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: value: {2}\r\n",
                new string(' ', Level),
                System.Reflection.MethodBase.GetCurrentMethod().Name,
                value);

        long value64 = BitConverter.DoubleToInt64Bits(value);

        for (int i = 0; i < 8; i++)
        {
            Data.Insert(Offset++,(byte)((value64 >> 8 * (7 - i)) & 0xff));
        }
    }

    public string ReadString(int Length)
    {
        Enter();

        byte[] rawStr = new byte[Length];
        Data.CopyTo(Offset, rawStr, 0, Length);
        string RetStr = Encoding.UTF8.GetString(rawStr);
        Offset += Length;

        Exit();
        return RetStr;
    }

    public void WriteString(string str)
    {
        foreach (byte b in Encoding.UTF8.GetBytes(str))
        {
            Data.Insert(Offset++, b);
        }
    }

    public uint ReadAMF3UInt()
    {
        Enter();

        uint Byte = (uint)ReadByte();

        if (Byte < 128)
        {
            Exit();
            return Byte;
        }
        else
        {
            Byte = (Byte & 0x7f) << 7;
            uint NewByte = (uint)ReadByte();

            if (NewByte < 128)
            {
                Exit();
                return Byte | NewByte;
            }
            else
            {
                Byte = (Byte | (NewByte & 0x7f)) << 7;
                NewByte = (uint)ReadByte();
                if (NewByte < 128)
                {
                    Exit();
                    return Byte | NewByte;
                }
                else
                {
                    Byte = (Byte | (NewByte & 0x7f)) << 8;
                    NewByte = (uint)ReadByte();

                    Byte |= NewByte;
                    Exit();
                    return Byte;
                }

            }
        }
    }

    public int ReadAMF3Int()
    {
        uint Data = ReadAMF3UInt();
        bool neg = (Data & 0x20000000) != 0;
        int Val = (int)Data & 0x1FFFFFFF;
        if (neg)
            Val = -Val;

        return Val;
    }

    public void WriteAMF3UInt(uint value)
    {
        if (value > 0x3FFFFFFF)
            throw new ArgumentOutOfRangeException("value");

        Enter();

        if (value < 128)
        {
            byte[] bytes = new byte[1];
            bytes[0] = (byte)value;

            Data.Insert(Offset++,bytes[0]);
        }
        else
        {
            //TODO: max 4 bytes, and if 4 bytes, the first one don't need `& 0x7f`
            byte[] tmp_bytes = new byte[5];

            uint tmp_value = value;
            int index = 0;
            while (tmp_value > 0)
            {
                tmp_bytes[index] = (byte) ( tmp_value & 0x7f );
                tmp_value >>= 7;
                index++;
            }

            for (int i = index - 1; i > 0; i--)
            {
                Data.Insert(Offset++, (byte) ( tmp_bytes[i] | 0x80 ) );
            }

            Data.Insert(Offset++, tmp_bytes[0]);
            //TODO: Add other cases
        }

        Exit();
    }

    public void WriteAMF3Int(int value)
    {
        //TODO: if out of range, use double
        if (value >= 0x10000000 || value < -0x10000000)
            throw new ArgumentOutOfRangeException("value");

        uint value2 = (uint)value & 0x1FFFFFFF;
        if (value < 0)
            value2 = (uint)value | 0x20000000;

        WriteAMF3UInt(value2);
    }

    public string ReadAMF3String()
    {
        Enter();

        if (_DebugLevel > 4)
            AddDebugMessage("{0}{1}: @0x{2:x}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Offset );

        uint StrRef = ReadAMF3UInt();
        string Str = "";

        if (_DebugLevel > 4)
            AddDebugMessage("{0} StrRef: 0x{2:x} {3}: 0x{4:x}\r\n",
                           Prefix,
                           System.Reflection.MethodBase.GetCurrentMethod().Name,
                           StrRef,
                           (StrRef&1)==1 ? "Length": "Index",
                           StrRef >> 1
                       );

        if ((StrRef & 1) == 1)
        {
            uint StrLen = StrRef >> 1;

            Str = ReadString((int)StrLen);

            if (!String.IsNullOrEmpty(Str) && StringRefs != null)
            {
                if (_DebugLevel > 4)
                    AddDebugMessage("{0} Str: [{2}] Refs Table Index: 0x{3:x}\r\n",
                        Prefix,
                        System.Reflection.MethodBase.GetCurrentMethod().Name,
                        Str,
                        StringRefs.Count
                    );
                StringRefs.Add(Str);
            }
            else
            {
                if (_DebugLevel > 4)
                    AddDebugMessage("{0} Str: [{2}]\r\n",
                        Prefix,
                        System.Reflection.MethodBase.GetCurrentMethod().Name,
                        Str
                    );
            }
        }
        else
        {
            uint j = StrRef >> 1;

            if (StringRefs != null)
            {
                Str = (string)StringRefs[(int)j];

                if (_DebugLevel > 4)
                    AddDebugMessage("{0} Str:[{2}] from Refs Table\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Str);

            }
        }

        Exit();
        return Str;
    }

    public void WriteAMF3String(string str)
    {
        Enter();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: [{2}] @0x{3:x}\r\n",
                    Prefix,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    str,
                    Offset
                );

        if (str != "")
        {
            for (int index = 0; index < NewStringRefs.Count; index++)
            {
                if (str == (string)NewStringRefs[index])
                {
                    if (_DebugLevel > 2)
                        AddDebugMessage("{0} Found {2}=={3} Index: 0x{4:x} Write: 0x{5:x}\r\n",
                                Prefix,
                                System.Reflection.MethodBase.GetCurrentMethod().Name,
                                str,
                                NewStringRefs[index],
                                index,
                                index << 1
                            );

                    WriteAMF3UInt((uint)(index << 1));
                    Exit();
                    return;
                }
            }
            NewStringRefs.Add(str);
        }

        WriteAMF3UInt( (uint) (Encoding.UTF8.GetByteCount(str) << 1) | 0x1);
        WriteString(str);


        Exit();
    }

    public AMF3Object ReadAMF3Object()
    {
        AMF3Object ReturnValue = new AMF3Object();

        Enter();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: @0x{2:x}\r\n", 
                Prefix, 
                System.Reflection.MethodBase.GetCurrentMethod().Name, 
                Offset);

        uint ReferencePtr = ReadAMF3UInt();

        if (_DebugLevel > 2)
            AddDebugMessage("{0} ReferencePtr: 0x{2:x}\r\n",
                    Prefix,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    ReferencePtr
                );

        ReturnValue.TypeIdentifier = "";
        ReturnValue.IsExternalizable = 0;
        ReturnValue.IsDynamic = 0;
        ReturnValue.ClassMemberCount = 0;
        ReturnValue.IsInLine = (ReferencePtr & 1);
        ReferencePtr >>= 1;

        if (_DebugLevel > 2)
            AddDebugMessage("{0} IsInLine: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsInLine);

        if (ReturnValue.IsInLine == 1)
        {
            ReturnValue.IsInLineClassDef = ReferencePtr & 1;
            ReferencePtr >>= 1;
            if (_DebugLevel > 2)
                AddDebugMessage("{0} IsInLineClassDef: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsInLineClassDef);

            if (ReturnValue.IsInLineClassDef == 1)
            {
                ReturnValue.TypeIdentifier = ReadAMF3String();
                ReturnValue.IsExternalizable = ReferencePtr & 1;

                ReferencePtr >>= 1;
                ReturnValue.IsDynamic = ReferencePtr & 1;
                ReferencePtr >>= 1;
                ReturnValue.ClassMemberCount = ReferencePtr;

                if (_DebugLevel > 0)
                {
                    AddDebugMessage("{0} TypeIdentifier: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.TypeIdentifier);
                    AddDebugMessage("{0} IsExternalizable: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsExternalizable);
                    AddDebugMessage("{0} IsDynamic: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.IsDynamic);
                    AddDebugMessage("{0} ClassMemberCount: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.ClassMemberCount);
                }

                for (int i = 0; i < ReturnValue.ClassMemberCount; i++)
                {
                    string ClassMemberDefinition = ReadAMF3String();
                    ReturnValue.ClassMemberDefinitions.Add(ClassMemberDefinition);

                    if (_DebugLevel > 2)
                        AddDebugMessage("{0} ClassMemberDefinition: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ClassMemberDefinition);
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
                if (_DebugLevel > 2)
                    AddDebugMessage("{0} ReferencePtr: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReferencePtr);

                if (ClassDefinitions != null)
                {
                    ClassDefinition OneClassDefinition = (ClassDefinition)ClassDefinitions[(int)ReferencePtr];

                    if (_DebugLevel > 2)
                        AddDebugMessage("{0} OneClassDefinition Found\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name );

                    ReturnValue.TypeIdentifier = OneClassDefinition.TypeIdentifier;
                    ReturnValue.ClassMemberDefinitions = OneClassDefinition.ClassMemberDefinitions;
                    ReturnValue.IsExternalizable = OneClassDefinition.IsExternalizable;
                    ReturnValue.IsDynamic = OneClassDefinition.IsDynamic;
                }
            }
        }
        else
        {
            AddDebugMessage(Prefix + "TODO: Use ReferencePtr\r\n\r\n");

            Exit();
            return ReturnValue;
        }

        if (ReturnValue.IsExternalizable == 1)
        {
            switch (ReturnValue.TypeIdentifier)
            {
                case "flex.messaging.io.ArrayCollection":
                case "flex.messaging.io.ObjectProxy":
                    MetaObject DataArray = ReadAMF3();
                    ReturnValue.Parameters.Add(DataArray);
                    break;

                case "DSK":
                    // skip two bytes
                    Offset += 2;
                    AddDebugMessage(String.Format("{0} TODO: Skip 2 bytes in {1}", Prefix, ReturnValue.TypeIdentifier));

                    // Read the inner type and add it
                    MetaObject StartType = ReadAMF3();
                    String StartClass = ((AMF3Object)StartType.Data).TypeIdentifier;
                    ReturnValue.ClassMemberDefinitions.Add(StartClass);
                    ReturnValue.Parameters.Add(StartType);
                    break;

                default:
                    AddDebugMessage(Prefix + "Can't read " + ReturnValue.TypeIdentifier);
                    break;
            }
        }
        else
        {
            for (int i = 0; i < ReturnValue.ClassMemberDefinitions.Count; i++)
            {

                if (_DebugLevel > 0)
                    AddDebugMessage(Prefix + ReturnValue.ClassMemberDefinitions[i].ToString() + "\r\n");

                ReturnValue.Parameters.Add(ReadAMF3()); //Value
            }

            if (ReturnValue.IsDynamic == 1)
            {
                string Key = ReadAMF3String(); //Key
                while (Key.Length > 0)
                {
                    if (_DebugLevel > 2)
                        AddDebugMessage("{0} Key: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Key);

                    ReturnValue.DynamicMembers.Add(Key, ReadAMF3()); //Value                    
                    Key = ReadAMF3String(); //Key
                }
            }
        }

        AddDebugMessage("{0} End Offset: 0x{1:x}\r\n", Prefix, Offset );
        Exit();
        return ReturnValue;
    }

    public void WriteAMF3Object(AMF3Object Value)
    {
        Enter();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: \r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name);

        uint ReferencePtr = 0;

        ReferencePtr |= Value.IsInLine;

        if (_DebugLevel > 2)
            AddDebugMessage("{0} IsInLine: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Value.IsInLine);

        if (Value.IsInLine == 1)
        {
            ReferencePtr |= Value.IsInLineClassDef << 1;

            if (_DebugLevel > 2)
                AddDebugMessage("{0} IsInLineClassDef: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Value.IsInLineClassDef);

            if (Value.IsInLineClassDef == 1)
            {
                ReferencePtr |= Value.IsExternalizable << 2;
                ReferencePtr |= Value.IsDynamic << 3;
                ReferencePtr |= Value.ClassMemberCount << 4;

                WriteAMF3UInt( (uint) ReferencePtr);
                WriteAMF3String( (string) Value.TypeIdentifier);

                for (int i = 0; i < Value.ClassMemberCount; i++)
                {
                    if (_DebugLevel > 2)
                        AddDebugMessage("{0} ClassMemberDefinition: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Value.ClassMemberDefinitions[i]);

                    WriteAMF3String( (string) Value.ClassMemberDefinitions[i]);
                }

                NewClassDefinitions.Add(
                    new ClassDefinition(
                        Value.TypeIdentifier,
                        Value.ClassMemberDefinitions,
                        Value.IsExternalizable,
                        Value.IsDynamic
                    )
                );
            }
            else
            {
                if (_DebugLevel > 2)
                    AddDebugMessage("{0} Finding Index {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReferencePtr);

                for (int index = 0; index < NewClassDefinitions.Count; index++)
                {
                    ClassDefinition OneClassDefinition = (ClassDefinition)NewClassDefinitions[index];

                    if (Value.TypeIdentifier == OneClassDefinition.TypeIdentifier)
                    {
                        ReferencePtr |= (uint)(index << 2);

                        if (_DebugLevel > 2)
                            AddDebugMessage("{0} Found Index: 0x{2:x} ReferencePtr: 0x{3:x}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, index, ReferencePtr);

                        
                        break;
                    }
                }

                WriteAMF3UInt((uint)ReferencePtr);
            }
        }
        else
        {
            WriteAMF3UInt((uint)ReferencePtr);
        }

        if (Value.IsExternalizable == 1)
        {
            //TODO: Write
            AddDebugMessage(Prefix + "TODO: Write Externalizable Object\r\n");
            switch (Value.TypeIdentifier)
            {
                case "flex.messaging.io.ArrayCollection":
                case "flex.messaging.io.ObjectProxy":
                    //MetaObject DataArray = WriteAMF3();
                    //Value.Parameters.Add(DataArray);
                    break;

                case "DSK":
                    // skip two bytes
                    //Offset += 2;

                    // Write the inner type and add it
                    //MetaObject StartType = WriteAMF3();
                    //String StartClass = ((AMF3Object)StartType.Data).TypeIdentifier;
                    //Value.ClassMemberDefinitions.Add(StartClass);
                    //Value.Parameters.Add(StartType);
                    break;

                default:
                    AddDebugMessage(Prefix + "Can't read " + Value.TypeIdentifier);
                    break;
            }
        }
        else
        {
            //TODO:
            if (_DebugLevel > 2)
                AddDebugMessage("{0} Value.ClassMemberDefinitions.Count: 0x{2:x}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Value.ClassMemberDefinitions.Count);

            foreach (MetaObject CurrentMetaObject in Value.Parameters)
            {
                if (_DebugLevel > 0)
                    AddDebugMessage("{0} WriteAMF3 for {2}\r\n", 
                            Prefix, 
                            System.Reflection.MethodBase.GetCurrentMethod().Name, 
                            CurrentMetaObject
                        );
                WriteAMF3(CurrentMetaObject);
            }

            if (Value.IsDynamic == 1)
            {
                foreach(KeyValuePair<string, Object> kvp in Value.DynamicMembers)
                {
                    if (_DebugLevel > 2)
                        AddDebugMessage("{0} Key: {2:G} - {3}\r\n",
                                Prefix,
                                System.Reflection.MethodBase.GetCurrentMethod().Name,
                                kvp.Key,
                                kvp.Value);

                    WriteAMF3String( (string) kvp.Key );
                    WriteAMF3( (MetaObject) kvp.Value );
                }

                WriteAMF3String("");
            }
        }
        Exit();
    }

    public ArrayList ReadAMF3Array()
    {
        Enter();

        uint ReferencePtr = ReadAMF3UInt();
        Dictionary<string, object> KeyValues = new Dictionary<string, object>();
        ArrayList DataList = new ArrayList();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: ReferencePtr: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReferencePtr);

        uint InLine = ReferencePtr & 1;
        ReferencePtr >>= 1;
        if (InLine != 0)
        {
            string Key = ReadAMF3String();
            while (Key.Length > 0)
            {
                KeyValues.Add(Key, ReadAMF3());
                if (_DebugLevel > 2)
                    AddDebugMessage(Key + "\r\n");
                Key = ReadAMF3String();
            }

            for (int i = 0; i < ReferencePtr; i++)
            {
                DataList.Add(ReadAMF3()); //Value
            }
        }
        else
        {
            //TODO: Use ref
            AddDebugMessage(Prefix + "TODO: Use Ref to Read AMF3Array\r\n");
        }

        ArrayList ReturnValues = new ArrayList();
        ReturnValues.Add(KeyValues);
        ReturnValues.Add(DataList);

        Exit();
        return ReturnValues;
    }

    public void WriteAMF3Array(ArrayList Values)
    {
        Enter();
        Dictionary<string, object> KeyValues = (Dictionary<string, object>) Values[0];
        ArrayList DataList = (ArrayList) Values[1];


        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: \r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name);

        WriteAMF3UInt((uint)((DataList.Count << 1) | 1));

        foreach (KeyValuePair<string, Object> kvp in KeyValues)
        {
            WriteAMF3String(kvp.Key);
            WriteAMF3( (MetaObject) kvp.Value);
        }
        WriteAMF3String("");

        for (int i = 0; i < DataList.Count; i++)
        {
            WriteAMF3( (MetaObject) DataList[i]);
        }

        Exit();
    }

    public DateTime ReadAMF3Date()
    {
        Enter();

        uint ReferencePtr = ReadAMF3UInt();
        bool InLine = (ReferencePtr & 0x1) == 0x1;
        ReferencePtr >>= 1;

        DateTime DateValue = new DateTime(1970, 1, 1);

        if (InLine)
        {
            if (_DebugLevel > 2)
                AddDebugMessage("{0}{1}: value: {2:F} @0x{3:x}\r\n{0} 0x{4:x} 0x{5:x} 0x{6:x} 0x{7:x}\r\n{0} 0x{8:x} 0x{9:x} 0x{10:x} 0x{11:x}\r\n",
                    Prefix,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    0,
                    Offset,
                    Data[Offset], Data[Offset + 1], Data[Offset + 2], Data[Offset + 3],
                    Data[Offset + 4], Data[Offset + 5], Data[Offset + 6], Data[Offset + 7]
                    );
            DateValue = DateValue.AddMilliseconds(ReadDouble());
        }
        else
        {
            //TODO: Referencing
            AddDebugMessage(Prefix + "TODO: Read Date using referencing\r\n");
        }
        Exit();
        return DateValue;
    }

    public void WriteAMF3Date(DateTime date_time) //TODO: Write
    {
        Enter();
        AddDebugMessage(Prefix + "TODO: WriteAMF3Date\r\n");
        Exit();
    }

    public MetaObject ReadAMF3()
    {
        //Flash 9+
        Enter();
        int Type = ReadByte();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: Type: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Type);

        MetaObject ReturnValue = new MetaObject();
        ReturnValue.Type = Type;
        ReturnValue.Data = null;
        ReturnValue.Offset = Offset;

        switch (Type)//marker
        {
            case 0x00://undefined-marker
                //undefined
                ReturnValue.TypeStr = "undefined";
                //ReturnValue.Data = "undefined";
                break;

            case 0x01://null-marker
                //null; 
                ReturnValue.TypeStr = "null";
                //ReturnValue.Data = "null";
                break;

            case 0x02://false-marker
                //boolean false
                ReturnValue.TypeStr = "false";
                //ReturnValue.Data = "false";
                break;

            case 0x03://true-marker
                //boolean true
                ReturnValue.TypeStr = "true";
                //ReturnValue.Data = "true";
                break;

            case 0x04://integer-marker
                //Amf3Int
                ReturnValue.TypeStr = "Amf3Int";
                ReturnValue.Data = ReadAMF3Int();
                if (_DebugLevel > 2)
                    AddDebugMessage("{0} Value: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.Data);
                break;

            case 0x05://double-marker
                ReturnValue.TypeStr = "Double";
                ReturnValue.Data = ReadDouble();
                break;

            case 0x06://string-marker
                //Amf3String
                ReturnValue.TypeStr = "Amf3String";
                ReturnValue.Data = ReadAMF3String();
                if (_DebugLevel > 2)
                    AddDebugMessage("{0} Value: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, ReturnValue.Data);
                break;

            case 0x07://xml-doc-marker
                ReturnValue.TypeStr = "Amf3XmlString";
                //TODO: Amf3XmlString
                if (_DebugLevel > 2)
                    AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3String\r\n");
                break;

            case 0x08://date-marker
                //Amf3Date
                ReturnValue.TypeStr = "Amf3Date";
                ReturnValue.Data = ReadAMF3Date();
                break;

            case 0x09: //OK//array-marker
                //Amf3Array
                ReturnValue.TypeStr = "Amf3Array";
                ReturnValue.Data = ReadAMF3Array();
                break;

            case 0x0A://object-marker
                //Amf3Object
                ReturnValue.TypeStr = "Amf3Object";
                ReturnValue.Data = ReadAMF3Object();
                break;

            case 0x0B://xml-marker
                //TODO: Amf3XmlString
                ReturnValue.TypeStr = "Amf3XmlString";

                if (_DebugLevel > 2)
                    AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3XmlString\r\n");
                break;

            case 0x0C://byte-array-marker
                //TODO: Amf3ByteArray
                ReturnValue.TypeStr = "Amf3ByteArray";

                if (_DebugLevel > 2)
                    AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3ByteArray\r\n");
                break;
            default:
            case 0x0D://vector-int-marker, Flash 10+
            case 0x0E://vector-uint-marker, Flash 10+
            case 0x0F://vector-double-marker, Flash 10+
            case 0x10://vector-object-marker, Flash 10+
            case 0x11://dictionary-marker, Flash 10+

                if (_DebugLevel > 2)
                    AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: " + Type + "\r\n");
                break;
        }

        Exit();
        return ReturnValue;
    }

    public void WriteAMF3(MetaObject Value)
    {
        Enter();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: Type: {2:G} Data: {3}\r\n",
                   Prefix,
                   System.Reflection.MethodBase.GetCurrentMethod().Name,
                   Value.Type,
                   Value.Data
                  );

        WriteByte( (byte) Value.Type);

        switch (Value.Type)
        {
            case 0x00:
                //undefined
                break;

            case 0x01:
                //null; 
                break;

            case 0x02:
                //boolean false
                break;

            case 0x03:
                //boolean true
                break;

            case 0x04:
                //Amf3Int
                if (_DebugLevel > 2)
                    AddDebugMessage("{0} Value: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Value.Data);

                WriteAMF3Int( (int) Value.Data);
                break;

            case 0x05:
                WriteDouble( (double) Value.Data);

                break;

            case 0x06:
                //Amf3String
                if (_DebugLevel > 2)
                    AddDebugMessage("{0} Value: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Value.Data);

                WriteAMF3String( (string) Value.Data);
                break;

            case 0x07:
                //TODO: Amf3XmlString
                if (_DebugLevel > 2)
                    AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3String\r\n");
                break;

            case 0x08:
                //Amf3Date
                WriteAMF3Date( (DateTime) Value.Data);
                break;

            case 0x09: //OK
                //Amf3Array
                WriteAMF3Array( (ArrayList) Value.Data);
                break;

            case 0x0A:
                //Amf3Object
                WriteAMF3Object( (AMF3Object) Value.Data);
                break;

            case 0x0B:
                //TODO: Amf3XmlString
                if (_DebugLevel > 2)
                    AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3XmlString\r\n");
                break;

            case 0x0C:
                //TODO: Amf3ByteArray
                if (_DebugLevel > 2)
                    AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + " TODO: Amf3ByteArray\r\n");
                break;
        }

        Exit();
    }

    public ArrayList ReadAMF0StrictArray()
    {
        Enter();

        ArrayList ReturnValues = new ArrayList();
        int Count = ReadLong();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: Offset: 0x{2:x} Length: 0x{3:x}\r\n",
                    Prefix,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    Offset,
                    Count);

        for (int i = 0; i < Count; i++)
        {
            if (_DebugLevel > 2)
                AddDebugMessage("{0} Array Offset: 0x{1:x}\r\n", Prefix, Offset);

            MetaObject analyzed_data = ReadAMF0();
            ReturnValues.Add(analyzed_data);
        }

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: End Offset: 0x{2:x}\r\n",
                    Prefix,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    Offset);

        Exit();
        return ReturnValues;
    }

    public void WriteAMF0StrictArray(ArrayList Values)
    {
        Enter();

        WriteLong(Values.Count);

        for (int i = 0; i < Values.Count; i++)
        {
            WriteAMF0( (MetaObject) Values[i]);
        }

        Exit();
    }

    public string ReadAMF0String()
    {
        Enter();

        int Length = ReadU16();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: Offset: 0x{2:x}\r\n",
                    Prefix,
                    System.Reflection.MethodBase.GetCurrentMethod().Name,
                    Offset);

        string Ret = ReadString(Length);
        
        if (_DebugLevel > 2)
            AddDebugMessage("{0} {1} Offset: 0x{2:x}\r\n", Prefix, Ret, Offset);

        Exit();
        return Ret;
    }

    public void WriteAMF0String(string str)
    {
        WriteU16(Encoding.UTF8.GetByteCount(str));
        WriteString(str);
    }

    public MetaObject ReadAMF0()
    {
        //Flash 6+
        Enter();
        MetaObject ReturnValue = new MetaObject();

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: @0x{2:x}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Offset );

        int Type = ReadByte();

        ReturnValue.Type = Type;
        ReturnValue.Data = null;
        ReturnValue.Offset = Offset;

        if (_DebugLevel > 2)
            AddDebugMessage("{0} Type: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Type);

        switch (Type)
        {
            case (int)AMFDataTypes.String:
                ReturnValue.TypeStr = "String";
                ReturnValue.Data = ReadAMF0String();
                break;

            case (int)AMFDataTypes.StrictArray:
                ReturnValue.TypeStr = "StrictArray";
                ReturnValue.Data = ReadAMF0StrictArray();
                break;

            case (int)AMFDataTypes.AMF3:
                //Flash 9+
                ReturnValue.TypeStr = "AMF3";
                ReturnValue.Data = ReadAMF3();
                break;

            case (int)AMFDataTypes.Number:
            case (int)AMFDataTypes.Boolean:
            case (int)AMFDataTypes.Object:
            case (int)AMFDataTypes.Null:
            case (int)AMFDataTypes.Undefined:
            case (int)AMFDataTypes.Reference:
            case (int)AMFDataTypes.MovieClip:
            case (int)AMFDataTypes.EcmaArray:
            case (int)AMFDataTypes.ObjectEnd:
            case (int)AMFDataTypes.RecordSet:
            case (int)AMFDataTypes.Date:
            case (int)AMFDataTypes.LongUTF:
            case (int)AMFDataTypes.Unsupported:
            case (int)AMFDataTypes.XMLDocument:
            case (int)AMFDataTypes.TypedObject:
            default:
                //TODO:
                ReturnValue.TypeStr = "";
                AddDebugMessage("{0} TODO: Implement AMF0 Type: {2}:{3}({4})@{5}\r\n",
                        Prefix,
                        System.Reflection.MethodBase.GetCurrentMethod().Name,
                        ReturnValue.Name,
                        ((AMFDataTypes)Type).ToString(),
                        Type.ToString(),
                        Offset
                    );

                break;

        }

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: End Offset: 0x{2:x}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Offset);

        Exit();
        return ReturnValue;
    }

    public void WriteAMF0(MetaObject Value)
    {
        Enter();
        WriteByte( (byte) Value.Type);

        if (_DebugLevel > 2)
            AddDebugMessage("{0}{1}: Type: {2:G}\r\n", Prefix, System.Reflection.MethodBase.GetCurrentMethod().Name, Value.Type);

        switch (Value.Type)
        {
            case (int)AMFDataTypes.Number:
                //TODO:
                Value.TypeStr = "Number";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.Boolean:
                //TODO:
                Value.TypeStr = "Boolean";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.String:
                Value.TypeStr = "String";
                WriteAMF0String( (string) Value.Data);
                break;

            case (int)AMFDataTypes.Object:
                //TODO:
                Value.TypeStr = "Object";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.Null:
                //TODO:
                Value.TypeStr = "Null";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.Undefined:
                //TODO:
                Value.TypeStr = "Undefined";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.Reference:
                //TODO:
                Value.TypeStr = "Reference";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.StrictArray:
                Value.TypeStr = "StrictArray";
                WriteAMF0StrictArray( (ArrayList) Value.Data);
                break;

            case (int)AMFDataTypes.Date:
                //TODO:
                Value.TypeStr = "Date";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.LongUTF:
                //TODO:
                Value.TypeStr = "LongUTF";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.Unsupported:
                //TODO:
                Value.TypeStr = "Unsupported";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.XMLDocument:
                //TODO:
                Value.TypeStr = "XML";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.TypedObject:
                //TODO:
                Value.TypeStr = "CustomClass";
                AddDebugMessage(Prefix + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + Value.Name + " TODO: \r\n");
                break;

            case (int)AMFDataTypes.AMF3:
                Value.TypeStr = "AMF3";
                WriteAMF3( (MetaObject) Value.Data);
                break;

        }

        Exit();
    }
}

public class MyTreeView : TreeView
{
    AMFDataParser RelatedAMFDataParser;
    ElementEdit AMFElementEdit = new ElementEdit();

    public MyTreeView()
    {
        MouseDoubleClick += new MouseEventHandler(MyTreeView_MouseDoubleClick);

        AMFElementEdit.StartPosition = FormStartPosition.CenterScreen;
    }

    public void SetAMFDataParser(AMFDataParser pRelatedAMFDataParser)
    {
        RelatedAMFDataParser = pRelatedAMFDataParser;
    }

    void MyTreeView_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        //Warning if only mode
        if (RelatedAMFDataParser.ReadOnly)
        {
            MessageBox.Show(String.Format("You need to check \"Unlock For Editing\" from context menu or by pressing F2 key from the Web Sessions list on the left pane to use this feature."));
            return;
        }

        string node_text = RelatedAMFDataParser.GetTreeNodeData(SelectedNode);

        if (node_text != null)
        {
            AMFElementEdit.EditText = node_text;

            if (AMFElementEdit.ShowDialog() == DialogResult.OK)
            {
                RelatedAMFDataParser.SetTreeNodeData(SelectedNode, AMFElementEdit.EditText);
            }
        }
    }
}

public class AMFRequestInspector : Inspector2, IRequestInspector2, IBaseInspector2
{
    HTTPRequestHeaders _headers;
    private byte[] _body;

    private MyTreeView AMFTreeView;
    bool IsAMFContent;
    AMFDataParser m_AMFDataParser;

    public AMFRequestInspector()
    {
        IsAMFContent = false;
        m_AMFDataParser = new AMFDataParser();
    }

    public bool bReadOnly
    {
        get
        {
#if DEBUG_MESSAGE_GENERATION
            MessageBox.Show(String.Format("m_AMFDataParser.ReadOnly:{0}", m_AMFDataParser.ReadOnly));
#endif
            return m_AMFDataParser.ReadOnly;
        }
        set
        {
#if DEBUG_MESSAGE_GENERATION
            MessageBox.Show(String.Format("Set m_AMFDataParser.ReadOnly:{0}", value));
#endif
            m_AMFDataParser.ReadOnly = value;
        }
    }

    public bool bDirty
    {
        get
        {
#if DEBUG_MESSAGE_GENERATION
            MessageBox.Show(String.Format("m_AMFDataParser.Dirty:{0}", m_AMFDataParser.Dirty));
#endif
            return m_AMFDataParser.Dirty;
        }
    }

    public override bool UnsetDirtyFlag()
    {
        m_AMFDataParser.Dirty = false;
        return false;
    }

    public override int GetOrder()
    {
        return 0;
    }

    public void Clear()
    {
        IsAMFContent = false;
        m_AMFDataParser.Dirty = false;
        AMFTreeView.Nodes.Clear();
    }

    public override void AddToTab(TabPage tabPage)
    {
        AMFTreeView = new MyTreeView();
        tabPage.Text = "AMF";
        tabPage.Controls.Add(AMFTreeView);
        AMFTreeView.Dock = DockStyle.Fill;

        AMFTreeView.SetAMFDataParser(m_AMFDataParser);
    }

    public override void ShowAboutBox()
    {
        MessageBox.Show(string.Format("AMFParser.dll::AMFParser Plugin for Fiddler2 by Jeong Wook (Matt) Oh." ), "About Inspector");
    }

    public HTTPRequestHeaders headers
    {
        get
        {
#if DEBUG_MESSAGE_GENERATION
            MessageBox.Show(String.Format("Get headers"));
#endif
            return null;    // Return null if your control doesn't allow header editing.
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
            if (!m_AMFDataParser.ReadOnly && m_AMFDataParser.Dirty)
            {
#if DEBUG_MESSAGE_GENERATION
                MessageBox.Show("return _body");
#endif

                m_AMFDataParser.WriteAMFPacket();

#if DEBUG_MESSAGE_GENERATION
                MessageBox.Show(String.Format("return _body length of {0}", m_AMFDataParser.DataBytes.Length ));
#endif

                return m_AMFDataParser.DataBytes;
            }

            return null;
        }

        set
        {
            AMFTreeView.Nodes.Clear();

            if ((_headers.Exists("Transfer-Encoding") || _headers.Exists("Content-Encoding")))
            {
                //lblDisplayMyEncodingWarning.Visible = true; 
                return;
            }

            _body = value;
            if (IsAMFContent)
            {
                try
                {
                    m_AMFDataParser.ReadAMFPacket(_body);

                    //Drawing
                    m_AMFDataParser.DrawOnTreeView(AMFTreeView);                    
                }
                catch (Exception e)
                {
                    m_AMFDataParser.DumpHexRemaining();
                }
            }
        }
    }

    public override int ScoreForContentType(string sMIMEType)
    {
        if (sMIMEType.OICEquals("application/x-amf"))
            return 1;
        else
            return 0;
    }
}

public class AMFResponseInspector : Inspector2, IResponseInspector2
{
    HTTPResponseHeaders _headers;
    private byte[] _body;

    private MyTreeView AMFTreeView;
    bool IsAMFContent;
    AMFDataParser m_AMFDataParser;

    public AMFResponseInspector()
    {
        m_AMFDataParser = new AMFDataParser();
        IsAMFContent = false;
    }

    public bool bReadOnly
    {
        get
        {
            return m_AMFDataParser.ReadOnly;
        }
        set
        {
            m_AMFDataParser.ReadOnly = value;
        }
    }

    public bool bDirty
    {
        get
        {
            return m_AMFDataParser.Dirty;
        }
    }

    public override int GetOrder()
    {
        return 0;
    }

    public void Clear()
    {
        IsAMFContent = false;
        m_AMFDataParser.Dirty = false;
    }

    public override void AddToTab(TabPage tabPage)
    {
        AMFTreeView = new MyTreeView();

        tabPage.Text = "AMF";
        tabPage.Controls.Add(AMFTreeView);
        AMFTreeView.Dock = DockStyle.Fill;

        AMFTreeView.SetAMFDataParser(m_AMFDataParser);
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

    public override void ShowAboutBox()
    {
        MessageBox.Show(string.Format("AMFParser.dll::AMFParser Plugin for Fiddler2 by Jeong Wook (Matt) Oh."), "About Inspector");
    }

    public byte[] body
    {
        get
        {
            return _body;
        }
        set
        {
            AMFTreeView.Nodes.Clear();

            if ((_headers.Exists("Transfer-Encoding") || _headers.Exists("Content-Encoding")))
            {
                // Create a copy of the body to avoid corrupting the original
                byte[] arrCopy = (byte[])value.Clone();
                try
                {
                    // Decode. Warning: Will throw if value cannot be decoded
                    Utilities.utilDecodeHTTPBody(_headers, ref arrCopy);
                    value = arrCopy;
                }
                catch
                {
                    // Leave value alone.
                    return;
                }
            }


            _body = value;

            if (IsAMFContent)
            {
                try
                {
                    m_AMFDataParser.ReadAMFPacket(_body);

                    //Drawing
                    m_AMFDataParser.DrawOnTreeView(AMFTreeView);
                }
                catch (Exception e)
                {
                    m_AMFDataParser.DumpHexRemaining();
                }
            }
        }
    }

    public override int ScoreForContentType(string sMIMEType)
    {
        if (sMIMEType.OICEquals("application/x-amf"))
            return 1;
        else
            return 0;
    }
}
