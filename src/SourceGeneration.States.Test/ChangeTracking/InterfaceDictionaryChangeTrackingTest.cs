﻿namespace SourceGeneration.States.Test.ChangeTracking;

[TestClass]
public class InterfaceDictionaryChangeTrackingTest
{
    [TestMethod]
    public void ValueDictionary_Add()
    {
        var tracking = ChangeTrackingProxyFactory.Create(new TrackingObject());

        tracking.IDictionaryOfValue.Add(1, 1);
        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsCascadingChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsCascadingChanged);

        ((ICascadingChangeTracking)tracking).AcceptChanges();
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsCascadingChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsCascadingChanged);
    }

    [TestMethod]
    public void ValueList_Remove()
    {
        var tracking = ChangeTrackingProxyFactory.Create(new TrackingObject()
        {
            IDictionaryOfValue = new Dictionary<int, int>
            {
                { 1, 1 },
            }
        });

        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsCascadingChanged);

        tracking.IDictionaryOfValue.Remove(1);
        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsCascadingChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsCascadingChanged);

        ((ICascadingChangeTracking)tracking).AcceptChanges();
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsCascadingChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsCascadingChanged);
    }

    [TestMethod]
    public void ValueList_ItemChanged()
    {
        var tracking = ChangeTrackingProxyFactory.Create(new TrackingObject()
        {
            IDictionaryOfValue = new Dictionary<int, int>
            {
                { 1, 1 },
            }
        });

        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsCascadingChanged);

        tracking.IDictionaryOfValue[1] = 2;
        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsCascadingChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsCascadingChanged);

        ((ICascadingChangeTracking)tracking).AcceptChanges();
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsCascadingChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfValue).IsCascadingChanged);
    }

    [TestMethod]
    public void ObjectDictionary_ItemChanged()
    {
        var tracking = ChangeTrackingProxyFactory.Create(new TrackingObject()
        {
            IDictionaryOfObject = new Dictionary<int, TrackingObject>
            {
                { 0, new TrackingObject() },
                { 1, new TrackingObject() }
            }
        });

        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsCascadingChanged);

        tracking.IDictionaryOfObject[0].IntProperty = 1;

        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking).IsCascadingChanged);

        Assert.IsTrue(((ICascadingChangeTracking)tracking.IDictionaryOfObject).IsChanged);
        Assert.IsTrue(((ICascadingChangeTracking)tracking.IDictionaryOfObject).IsCascadingChanged);

        Assert.IsTrue(((ICascadingChangeTracking)tracking.IDictionaryOfObject[0]).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject[0]).IsCascadingChanged);

        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject[1]).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject[1]).IsCascadingChanged);

        ((ICascadingChangeTracking)tracking).AcceptChanges();

        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking).IsCascadingChanged);

        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject).IsCascadingChanged);

        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject[0]).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject[0]).IsCascadingChanged);

        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject[1]).IsChanged);
        Assert.IsFalse(((ICascadingChangeTracking)tracking.IDictionaryOfObject[1]).IsCascadingChanged);
    }

}
