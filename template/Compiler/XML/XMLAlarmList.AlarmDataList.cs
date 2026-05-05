//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Alarmlist.Compiler.XML;
//using Alarmlist.Syntax;

//namespace Alarmlist.Compiler.XML
//{
//    public partial class XMLAlarmList
//    {
//        public AlarmList ToErrorDataList()
//        {

//            return null;
////            var green = new SortedList<string, MergedAlarm>();

////            //import all normal alarms
////            foreach (var item in this.Alarms)
////            {                                
////                var i = new MergedAlarm(item,this.FullFilePath);
////                var key = i.ID;

////                if(green.ContainsKey(key))
////                    green[key].AddDublicate(i);
////                else
////                    green.Add(i.ID, i);
////            }
////            //
////            //import all errocodes referenced in another file
////            foreach (var referencedFile in this.Templates)
////            {
////                string targetFilepath = ResolvePath(this.FullFilePath, referencedFile.File);
////                var targetContent = default(AlarmList);

////                //load the file and get its content
////                try
////                {
////                    var fileContent = new XMLAlarmlistSerilaizer().Deserialize(targetFilepath);
////                    targetContent = fileContent.ToErrorDataList();
////                }
////                catch (Exception ex)
////                {
////                    throw AlarmlistFactory.ReferenceException(referencedFile, ex);
////                }

////                foreach (var item in referencedFile.Children)
////                {
////                    //get alarm from source file
////                    var temp = default(MergedAlarm);
////                    var toBeImported = targetContent.Where(x => x.Code == item.TemplateID).FirstOrDefault();
////                    if(toBeImported != null)
////                    {
////                        var parent = new MergedAlarm(toBeImported, targetFilepath);
////                        temp = new MergedAlarm(item);
////                        temp.SetParent(parent);
////                    }
////                    else
////                    {
////                        temp = new MergedAlarm(item, targetFilepath, isMissing: true);
////                    }

////                    //check if the item already exists
////                    var key = item.Code;
////                    if (green.ContainsKey(key))
////                    {
////                        var existingItem = green[key];

////                        if (existingItem.IsMissing && !temp.IsMissing)
////                        {
////                            existingItem.IsNotMissed(temp);
////                        }
////                        else
////                        {
////                            existingItem.AddDublicate(temp);
////                        }

////                    }
////                    else
////                    {
////                         green.Add(temp.ID, temp);
////                    }
////                }                
////            }
////            //
////            //import all templated alarms
////            foreach (var item in this.Imports)
////            {
////                var temp = new MergedAlarm(item);
////                var key = temp.ID;
////                var parentkey = temp.ParentID;

////                //check if parent exists
////                MergedAlarm parent;
////                if (!green.ContainsKey(parentkey))
////                {
////                    parent = new MergedAlarm(parentkey, isMissing: true);
////                    green.Add(parentkey, parent);
////                }
////                else
////                {
////                    parent = green[parentkey];
////                }

////                //check if the item already exists
////                if (green.ContainsKey(key))
////                {
////                    var existingItem= green[key];

////                    if(existingItem.IsMissing)
////                    {
////                        existingItem.SetParent(parent);
////                        existingItem.IsNotMissed(temp);
////                    }
////                    else
////                    {
////                        existingItem.AddDublicate(temp);
////                    }
                    
////                }
////                else
////                {
////                    temp.SetParent(parent);
////                    green.Add(temp.ID, temp);
////                }                
                
////            }

////            var flat = green.Values.ToArray();

////            //validate that no circle dependancies exist.
////            var alreadyChecked = new List<MergedAlarm>();
////            foreach (var item in flat)
////            {
////                if (alreadyChecked.Contains(item)) continue;

////                var dic = new List<MergedAlarm>()
////                {
////                    item,//add startitem
////                };

////                var current = item.GetParent();
////                bool isCircle = false;
////                while (current != null && !isCircle)
////                {
////                    //add currently checked item
////                    dic.Add(current);
////                    alreadyChecked.Add(current);

////                    isCircle = item == current;

////#if DEBUG
////                    //beaucse we have a circe dependancy we can't look at the item while debuggging, because the circle creates a stackOverflow exception when trying to use inteli context.
////                    //To enable inteliContext we break the circle.
////                    if (isCircle) current.SetParent(null);
////#endif

////                    current = current.GetParent();
////                }

////                if (isCircle)
////                {                                        
////                    throw AlarmlistFactory.CircleDependancyException(item, dic);
////                }
////            }

////            //validate that the collection does not contain any missed references.
////            if (flat.Any( x=> x.IsMissing))
////            {
////                var item = flat.Where(x => x.IsMissing).First();
////                throw AlarmlistFactory.NoReferenceException(item);
////            }
////            //validate that no duplactes are in the collection.
////            else if(flat.Any( x => x.IsDublicate))
////            {
////                var item = flat.Where(x => x.IsDublicate).First();
////                throw AlarmlistFactory.DuplicateErrocodeException(item);
////            }

////            //export the flat list to actual data
////            var list = new AlarmList();
////            list.AddRange(flat.Select(x => new Alarm()
////            {
////                Code = x.ID,
////                Name = x.Name,
////                Description = x.Description,
////                Category = x.Category,
////                SolutionList = x.Solutions
////            }));
////            return list;
//        }

//        internal static string ResolvePath( string source, string referencedFile )
//        {
//            var result = referencedFile;
            

//            if (!Path.IsPathRooted(referencedFile))
//            {
//                result = source == null ? string.Empty : 
//                    Path.GetDirectoryName(source) ?? source;
//                result = Path.Combine(result, referencedFile);
//            }

//            return Path.GetFullPath(result);
//        }

//        [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
//        internal class MergedAlarm
//        {
//            private string GetDebuggerDisplay()
//            {
//                return this.GetType().Name + " " + this.ToString();
//            }

//            private readonly List<MergedAlarm> _dublicates = new List<MergedAlarm>();
//            private readonly List<MergedAlarm> _children = new List<MergedAlarm>();
//            private MergedAlarm _parent = default;

//            private string _name, _description, _category;
//            private XMLSolutionList _solutions;


//            public MergedAlarm(Alarm alarm, string sourceFile) : this(alarm.Code, alarm.Name, alarm.Description, null, alarm.Category, null)
//            {
//                this.SourceFile = sourceFile;
//                this._solutions = new XMLSolutionList() 
//                {
//                    ClearContent = null, //don't clear the content
//                    Items= new List<XMLAlarmSolution>( alarm.SolutionList.Select(x=> new XMLAlarmSolution() { Cause= x.Cause, Solutions= x.Solutions}))
//                };
//            }
//            public MergedAlarm(XMLImportAlarm alarm, string sourceFile, bool isMissing) : this(alarm.Code, alarm.Name, alarm.Description, alarm.TemplateID, alarm.Category, alarm.Solutions)
//            {
//                this.IsMissing = isMissing;
//                this.SourceFile = sourceFile;
//            }

//            public MergedAlarm(XMLAlarm alarm,string sourceFile=null):this(alarm, isMissing: false)
//            { 
//                this.SourceFile=sourceFile;
//            }
//            public MergedAlarm(XMLAlarm alarm,bool isMissing, string sourceFile = null) : this(alarm.Code, alarm.Name, alarm.Description, null, alarm.Category, alarm.Solutions)
//            { 
//                this.IsMissing = isMissing;
//                this.SourceFile = sourceFile;
//            }

//            public MergedAlarm(XMLImportAlarm alarm) : this(alarm, isMissing: false)
//            { }
//            public MergedAlarm(XMLImportAlarm alarm, bool isMissing) : this(alarm.Code, alarm.Name, alarm.Description, alarm.TemplateID,alarm.Category, alarm.Solutions)
//            {
//                this.IsMissing = isMissing;
//            }

//            public MergedAlarm(string iD, bool isMissing) : this(iD, null, null, null, null, null)
//            {
//                this.IsMissing = isMissing;
//            }

//            public MergedAlarm(string iD, string name, string description, string parentID,string category, XMLSolutionList solutions)
//            {
//                ID = iD;
//                _name = name;
//                _description = description;
//                _category = category;
//                _solutions = solutions;
//                ParentID = parentID;
//            }


//            public string ID { get; }

//            public string Name { get => _name ?? _parent?.Name; }

//            public string Description { get => _description ?? _parent?.Description; }

//            public string Category { get => _category ?? _parent?.Category; }

//            public string ParentID { get; private set; }

//            //public AlarmSolutionList Solutions => GetErrorSolutions();

//            public bool IsDublicate { get => _dublicates.Count>0; }

//            public bool IsMissing { get; private set; }

//            public string SourceFile { get; private set; }


//            private AlarmSolutionList GetErrorSolutions()
//            {
//                var clear = this._solutions?.IsClearRequested ?? false;

//                var list= new AlarmSolutionList();

//                // add parent solutions
//                if (!clear)
//                {
//                    _parent?.Solutions.ForEach(item => list.Add(item));
//                }

//                _solutions?.Items.ForEach(item => list.Add(AlarmlistFactory.GetErrorSolution(item)));

//                return list;
//            }

            

//            //public void IsMissed() => IsMissing = true;
//            public void IsNotMissed(MergedAlarm source)
//            {
//                _name = source.Name;
//                _description = source.Description;
//                ParentID = source.ParentID;

//                IsMissing = false;
//            }
            

//            public void AddDublicate(MergedAlarm item)
//            {
//                this._dublicates.Add(item);
//            }
//            public IEnumerable<MergedAlarm> GetDuplicates()
//            {
//                return this._dublicates;
//            }

//            public void SetParent( MergedAlarm parent)
//            {
//                this._parent?._children.Remove(this);
//                this._parent = parent;
//                parent?._children.Add(this);
//            }

//            internal MergedAlarm GetParent() => this._parent;

//            public IEnumerable<MergedAlarm> GetChildren()
//            {
//                return this._children;
//            }

//            public override string ToString()
//            {
//                return string.Format("id:{0} name:{1} Parent:{2}", this.ID, this.Name, this.ParentID);
//            }
//        }
//    }
//}
