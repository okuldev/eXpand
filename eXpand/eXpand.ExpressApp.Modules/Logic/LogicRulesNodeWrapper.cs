using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.NodeWrappers;
using eXpand.Persistent.Base.General;

namespace eXpand.ExpressApp.Logic {
    public abstract class LogicRulesNodeWrapper<TLogicRule> : NodeWrapper where TLogicRule:ILogicRule
    {
        protected LogicRulesNodeWrapper(DictionaryNode dictionaryNode) : base(dictionaryNode) {
        }


        protected abstract string ChildNodeName { get; }

        public List<TLogicRule> Rules{
            get { return GetRules(); }
        }

        internal TypesInfo TypesInfo { get; set; }


        public virtual TLogicRule AddRule(TLogicRule logicRuleAttribute, ITypeInfo typeInfo, Type logicRuleNodeWrapperType)
        {
            
            var stateRuleNodeWrapper =
                (TLogicRule)
                Activator.CreateInstance(logicRuleNodeWrapperType, new[] { Node.AddChildNode(ChildNodeName) });
            stateRuleNodeWrapper.ViewType = logicRuleAttribute.ViewType;
            stateRuleNodeWrapper.Nesting = logicRuleAttribute.Nesting;
            stateRuleNodeWrapper.Nesting = logicRuleAttribute.Nesting;
            stateRuleNodeWrapper.ID = logicRuleAttribute.ID;
            stateRuleNodeWrapper.TypeInfo = typeInfo;
            stateRuleNodeWrapper.Description = logicRuleAttribute.Description;
            stateRuleNodeWrapper.ViewId = logicRuleAttribute.ViewId;
            stateRuleNodeWrapper.Index = logicRuleAttribute.Index;
            return stateRuleNodeWrapper;
        }


        public IEnumerable<TLogicRule> FindRules(ITypeInfo typeInfo)
        {
            if (typeInfo != null) {
                foreach (TLogicRule rule in Rules.Where(rule => rule.TypeInfo == typeInfo)){
                    yield return rule;
                }
            }
        }

        protected virtual List<TLogicRule> GetRules()
        {
//            var single = AppDomain.CurrentDomain.GetTypes(typeof(LogicRuleNodeWrapper)).Where(type => !(type.IsAbstract)&&typeof(TLogicRule).IsAssignableFrom(type)).Single();
            return
                Node.ChildNodes.GetOrderedByIndex().Select(node =>(TLogicRule)Activator.CreateInstance(TypesInfo.LogicRuleNodeWrapperType, new[] { node })).ToList();
        }
    }
}