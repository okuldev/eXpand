using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base.Security;
using eXpand.ExpressApp.Core.DictionaryHelpers;
using eXpand.Persistent.Base.General;

namespace eXpand.ExpressApp.Logic {
    public abstract class LogicRuleProviderModuleBase<TLogicRule>:ModuleBase where TLogicRule:ILogicRule{
        readonly TypesInfo _typesInfo;
        public event EventHandler<CollectedRuleFromModelEventArgs<TLogicRule>> CollectedRulesFromModel;

        protected LogicRuleProviderModuleBase() {
            _typesInfo = new TypesInfo();
            _typesInfo.RegisterTypes<TLogicRule>();
        }

        protected virtual void InvokeCollectedRulesFromModel(CollectedRuleFromModelEventArgs<TLogicRule> e) {
            EventHandler<CollectedRuleFromModelEventArgs<TLogicRule>> handler = CollectedRulesFromModel;
            if (handler != null) handler(this, e);
        }

        public abstract string LogicRulesNodeAttributeName { get; }
        
        void CreateLogicRulesFromClassAttributes(LogicRulesNodeWrapper<TLogicRule> wrapper, ITypeInfo typeInfo) {
            var findAttributes = LogicRuleManager<TLogicRule>.FindAttributes(typeInfo).ToList();
            foreach (TLogicRule attribute in findAttributes) {
                TLogicRule attribute1 = attribute;
                if (LogicRuleManager<TLogicRule>.Instance[typeInfo].Where(rule => rule.ID==attribute1.ID).Count()==0) {
                    var logicRule = (TLogicRule)Activator.CreateInstance(_typesInfo.LogicRuleType, new object[] { wrapper.AddRule(attribute, typeInfo, _typesInfo.LogicRuleNodeWrapperType) });
                    OnRuleAdded(logicRule, attribute);
                }
            }
        }

        protected virtual void OnRuleAdded(TLogicRule logicRuleNodeWrapper, TLogicRule attribute)
        {
        }

        public void CollectRules(XafApplication xafApplication) {
            lock (LogicRuleManager<TLogicRule>.Instance)
            {
                SecuritySystem.ReloadPermissions();
                LogicRulesNodeWrapper<TLogicRule> wrapper = CreateModelWrapper(xafApplication.Model);
                
                foreach (ITypeInfo typeInfo in XafTypesInfo.Instance.PersistentTypes) {
                    LogicRuleManager<TLogicRule>.Instance[typeInfo] = null;
                    List<TLogicRule> enumerable = CollectRulesFromModelCore(wrapper, typeInfo).ToList();
                    InvokeCollectedRulesFromModel(new CollectedRuleFromModelEventArgs<TLogicRule>(enumerable));
                    List<TLogicRule> permissions =CollectRulesFromPermissions(typeInfo).ToList();
                    enumerable.AddRange(permissions);
                    LogicRuleManager<TLogicRule>.Instance[typeInfo] = new List<TLogicRule>(enumerable.OrderByDescending(rule => rule.Index));
                }
            }
        }

        public virtual IEnumerable<TLogicRule> CollectRulesFromPermissions(ITypeInfo typeInfo) {
            if (SecuritySystem.Instance is ISecurityComplex)
                if (SecuritySystem.CurrentUser != null) {
                    IList<IPermission> permissions = ((IUser) SecuritySystem.CurrentUser).Permissions;
                    return
                        permissions.OfType<TLogicRule>().Where(
                                                                  permission =>
                                                                  permission.TypeInfo != null &&
                                                                  permission.TypeInfo.Type == typeInfo.Type).OfType
                            <TLogicRule>();
                }
            return new List<TLogicRule>();
        }

        protected virtual IEnumerable<TLogicRule> CollectRulesFromModelCore(LogicRulesNodeWrapper<TLogicRule> wrapper,
                                                                            ITypeInfo typeInfo) {
            IEnumerable<TLogicRule> ruleNodeWrappers = wrapper.FindRules(typeInfo);
            
            return
                ruleNodeWrappers.Select(rule => Activator.CreateInstance(_typesInfo.LogicRuleType, rule)).Select(modelRule => modelRule).Cast<TLogicRule>();
        }

        public override void Setup(XafApplication application) {
            base.Setup(application);
            application.SetupComplete += (sender, args) => CollectRules(application);
            application.LoggedOn += (o, eventArgs) => CollectRules(application);
        }

        void CreateLogicRulesFromMethodsAttributes(LogicRulesNodeWrapper<TLogicRule> wrapper, ITypeInfo typeInfo){
            foreach (MethodInfo methodInfo in typeInfo.Type.GetMethods(LogicRuleManager<TLogicRule>.MethodRuleBindingFlags)) {
                foreach (TLogicRule attribute in LogicRuleManager<TLogicRule>.FindAttributes(methodInfo)) {
                    wrapper.AddRule(attribute, typeInfo, _typesInfo.LogicRuleNodeWrapperType);
                }
            }
        }

        public override void UpdateModel(Dictionary model) {
            base.UpdateModel(model);
            LogicRulesNodeWrapper<TLogicRule> wrapper = CreateModelWrapper(model);
            foreach (ITypeInfo typeInfo in XafTypesInfo.Instance.PersistentTypes) {
                CreateLogicRulesFromClassAttributes(wrapper, typeInfo);
                CreateLogicRulesFromMethodsAttributes(wrapper, typeInfo);
            }
        }

        LogicRulesNodeWrapper<TLogicRule> CreateModelWrapper(Dictionary dictionary)
        {
            DictionaryNode dictionaryNode = GetRootNode(dictionary).GetChildNode(LogicRulesNodeAttributeName);

            var logicRulesNodeWrapper = (LogicRulesNodeWrapper<TLogicRule>)
                                                          Activator.CreateInstance(_typesInfo.LogicRulesNodeWrapperType,
                                                                                   dictionaryNode.GetChildNode(GetElementGroupNodeName()));
            logicRulesNodeWrapper.TypesInfo = _typesInfo;
            return logicRulesNodeWrapper;
        }

        public virtual DictionaryNode GetRootNode(Dictionary dictionary) {
            return dictionary.RootNode;
        }



        public override Schema GetSchema() {
            var schemaHelper = new SchemaHelper();
            schemaHelper.AttibuteCreating += (sender, args) => modifyAttributes(args);
            string CommonTypeInfos = @"<Element Name=""" + LogicRulesNodeAttributeName +@""">
                                            <Element Name=""" +GetElementGroupNodeName() +@""">
                                                <Element Name=""" +GetElementNodeName() +@""" KeyAttribute=""ID"" Multiple=""True"">
                                                    " +schemaHelper.Serialize<ILogicRule>(true) +@"
                                                    " + GetMoreSchema() +@"
				                                </Element>
                                            </Element>
                                        </Element>";
            var schema = new Schema(schemaHelper.Inject(CommonTypeInfos, ModelElement.Application));
            return schema;
        }


        void modifyAttributes(AttibuteCreatedEventArgs args) {
            if (args.Attribute.IndexOf("Nesting") > -1 || args.Attribute.IndexOf("ViewType") > -1)
                args.AddTag(@" IsInvisible=""{" + typeof (ViewVisibilityCalculator).FullName +
                            @"}ID=..\..\@ID;ViewType=" + ViewType.Any + @"""");
            else if (args.Attribute.IndexOf("ID") > -1)
                args.AddTag(@" Required=""True""");
            else if (args.Attribute.IndexOf("TypeInfo") > -1)
                args.AddTag(@"Required=""True"" RefNodeName=""/Application/BOModel/Class""");
        }

        protected virtual string GetMoreSchema() {
            return new SchemaHelper().Serialize<TLogicRule>(true);
        }

        public abstract string GetElementNodeName();

        public virtual string GetElementGroupNodeName() {
            return "Rules";
        }

        public bool HasRules(View view) {
            return LogicRuleManager<TLogicRule>.HasRules(view);
        }
    }
}