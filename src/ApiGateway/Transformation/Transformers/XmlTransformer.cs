using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

public class XmlTransformer : IContentTransformer
{
    public string ContentType => "application/xml";

    public async Task<Stream> TransformAsync(
        Stream content,
        TransformationConfig config)
    {
        var doc = new XmlDocument();
        await Task.Run(() => doc.Load(content));

        var transformed = TransformNode(doc.DocumentElement, config);
        var resultDoc = new XmlDocument();
        var importedNode = resultDoc.ImportNode(transformed, true);
        resultDoc.AppendChild(importedNode);

        var resultStream = new MemoryStream();
        resultDoc.Save(resultStream);
        resultStream.Position = 0;
        return resultStream;
    }

    private XmlNode TransformNode(
        XmlNode node,
        TransformationConfig config)
    {
        var doc = new XmlDocument();
        var transformed = doc.CreateElement(
            MapNodeName(node.Name, config.Mappings));

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Element)
            {
                var childName = MapNodeName(
                    child.Name, 
                    config.Mappings);
                
                if (ShouldIncludeNode(childName, config))
                {
                    var transformedChild = TransformNode(
                        child, 
                        config);
                    var importedChild = doc.ImportNode(
                        transformedChild, 
                        true);
                    transformed.AppendChild(importedChild);
                }
            }
            else
            {
                var importedChild = doc.ImportNode(child, true);
                transformed.AppendChild(importedChild);
            }
        }

        foreach (XmlAttribute attr in node.Attributes)
        {
            var attrName = MapNodeName(attr.Name, config.Mappings);
            if (ShouldIncludeNode(attrName, config))
            {
                var transformedAttr = doc.CreateAttribute(attrName);
                transformedAttr.Value = attr.Value;
                transformed.Attributes.Append(transformedAttr);
            }
        }

        return transformed;
    }

    private static string MapNodeName(
        string originalName,
        Dictionary<string, string> mappings)
    {
        return mappings?.GetValueOrDefault(originalName) ?? originalName;
    }

    private static bool ShouldIncludeNode(
        string nodeName,
        TransformationConfig config)
    {
        if (config.FieldsToKeep?.Length > 0)
        {
            return config.FieldsToKeep.Contains(
                nodeName, 
                StringComparer.OrdinalIgnoreCase);
        }

        if (config.FieldsToRemove?.Length > 0)
        {
            return !config.FieldsToRemove.Contains(
                nodeName, 
                StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }
} 