﻿namespace AltCover

open System
open System.Xml.Linq

module Cobertura =
  let internal path : Option<string> ref = ref None
  let X = OpenCover.X

  let NCover (report:XDocument) (packages:XElement) =
    let (hxxx, txxx) =
                    report.Descendants(X "module")
                    |> Seq.fold (fun (h0, t0) m -> let package = XElement(X "package",
                                                                          XAttribute(X "name", m.Attribute(X "name").Value))
                                                   packages.Add(package)
                                                   let classes = XElement(X "classes")
                                                   package.Add(classes)
                                                   let (h0x, t0x) = m.Descendants(X "method")
                                                                    |> Seq.groupBy(fun mx -> (mx.Attribute(X "class").Value,
                                                                                              mx.Descendants(X "seqpnt")
                                                                                              |> Seq.map (fun s -> s.Attribute(X "document").Value)
                                                                                              |> Seq.head))
                                                                    |> Seq.sortBy fst
                                                                    |> Seq.fold (fun (h0', t0') ((n,s),mx) -> let cx = XElement(X "class",
                                                                                                                                XAttribute(X "name", n),
                                                                                                                                XAttribute(X "filename", s))
                                                                                                              classes.Add(cx)
                                                                                                              let mxx = XElement(X "methods")
                                                                                                              cx.Add(mxx)
                                                                                                              let (hxx, txx) = mx
                                                                                                                               |> Seq.map(fun mt -> let fn = mt.Attribute(X "fullname").Value.Split([| ' '; '(' |]) |> Array.toList
                                                                                                                                                    let key = fn.[1].Substring(n.Length + 1)
                                                                                                                                                    let signa = fn.[0] + " " + fn.[2]
                                                                                                                                                    (key, (signa, mt)))
                                                                                                                               |> Seq.sortBy fst
                                                                                                                               |> Seq.fold(fun (h', t') (key, (signa, mt)) -> let mtx = XElement(X "method",
                                                                                                                                                                                                 XAttribute(X "name", key),
                                                                                                                                                                                                 XAttribute(X "signature", signa))
                                                                                                                                                                              mxx.Add(mtx)
                                                                                                                                                                              let lines = XElement(X "lines")
                                                                                                                                                                              mtx.Add(lines)
                                                                                                                                                                              let (hx,tx) = mt.Descendants(X "seqpnt")
                                                                                                                                                                                             |> Seq.fold (fun (h,t) s -> let vc = s.Attribute(X "visitcount")
                                                                                                                                                                                                                         let vx = if isNull vc then "0" else vc.Value
                                                                                                                                                                                                                         let line = XElement(X "line",
                                                                                                                                                                                                                                        XAttribute(X "number", s.Attribute(X "line").Value),
                                                                                                                                                                                                                                        XAttribute(X "hits", if isNull vc then "0" else vc.Value),
                                                                                                                                                                                                                                        XAttribute(X "branch", "false"))
                                                                                                                                                                                                                         lines.Add line
                                                                                                                                                                                                                         (h + (if vx = "0" then 0 else 1), t + 1)) (0,0)
                                                                                                                                                                              if tx > 0 then mtx.SetAttributeValue(X "line-rate", (float hx)/(float tx))
                                                                                                                                                                              (h' + hx, t' + tx)) (0,0)
                                                                                                              if txx > 0 then cx.SetAttributeValue(X "line-rate", (float hxx)/(float txx))
                                                                                                              (h0'+hxx, t0'+txx)) (0,0)
                                                   if t0x > 0 then package.SetAttributeValue(X "line-rate", (float h0x)/(float t0x))
                                                   (h0 + h0x, t0 + t0x)) (0,0)
    if txxx > 0 then packages.Parent.SetAttributeValue(X "line-rate", (float hxxx)/(float txxx))
    packages.Parent.SetAttributeValue(X "branch-rate", null)

  let OpenCover (report:XDocument)  (packages:XElement) =
    let extract (owner:XElement) (target:XElement) =
        let summary = owner.Descendants(X "Summary") |> Seq.head
        let b = summary.Attribute(X "numBranchPoints").Value |> Int32.TryParse |> snd
        let bv = summary.Attribute(X "visitedBranchPoints").Value |> Int32.TryParse |> snd
        let s = summary.Attribute(X "numSequencePoints").Value |> Int32.TryParse |> snd
        let sv = summary.Attribute(X "visitedSequencePoints").Value |> Int32.TryParse |> snd
        if s > 0 then target.SetAttributeValue(X "line-rate", (float sv)/(float s))
        if b > 0 then target.SetAttributeValue(X "branch-rate", (float bv)/(float b))
    report.Descendants(X "Module")
    |> Seq.filter(fun m -> m.Descendants(X "Class") |> Seq.isEmpty |> not)
    |> Seq.iter (fun m -> let mname = m.Descendants(X "ModuleName")
                                      |> Seq.map (fun x -> x.Value)
                                      |> Seq.head
                          let package = XElement(X "package",
                                                 XAttribute(X "name", mname))
                          let files = m.Descendants(X "File")
                                      |> Seq.fold(fun m x -> m |>
                                                             Map.add (x.Attribute(X "uid").Value) (x.Attribute(X "fullPath").Value)) Map.empty
                          packages.Add(package)
                          let classes = XElement(X "classes")
                          package.Add(classes)

                          extract m package

                          m.Descendants(X "Method")
                          |> Seq.filter(fun m -> m.Descendants(X "FileRef") |> Seq.isEmpty |> not)
                          |> Seq.groupBy(fun mx -> ((mx.Parent.Parent.Descendants(X "FullName") |> Seq.head).Value,
                                                    mx.Descendants(X "FileRef")
                                                    |> Seq.map (fun s -> files
                                                                         |> Map.find (s.Attribute(X "uid").Value))
                                                    |> Seq.head))
                          |> Seq.sortBy fst
                          |> Seq.iter (fun ((n,s),mx) -> let cx = XElement(X "class",
                                                                           XAttribute(X "name", n),
                                                                           XAttribute(X "filename", s))
                                                         classes.Add(cx)
                                                         let mxx = XElement(X "methods")
                                                         cx.Add(mxx)
                                                         let q = mx
                                                                 |> Seq.map(fun mt -> let fn = (mt.Descendants(X "Name") |> Seq.head).Value.Split([| ' '; '(' |]) |> Array.toList
                                                                                      let key = fn.[1].Substring(n.Length + 2)
                                                                                      let signa = fn.[0] + " " + fn.[2]
                                                                                      (key, (signa, mt)))
                                                                 |> Seq.sortBy fst
                                                                 |> Seq.filter (fun (_,(_,mt)) -> mt.Descendants(X "SequencePoint") |> Seq.isEmpty |> not)
                                                                 |> Seq.fold(fun (b,bv,s,sv) (key, (signa, mt)) -> let mtx = XElement(X "method",
                                                                                                                                      XAttribute(X "name", key),
                                                                                                                                      XAttribute(X "signature", signa))
                                                                                                                   extract mt mtx
                                                                                                                   mxx.Add(mtx)
                                                                                                                   let lines = XElement(X "lines")
                                                                                                                   mtx.Add(lines)
                                                                                                                   let summary = mt.Descendants(X "Summary") |> Seq.head
                                                                                                                   ( b + (summary.Attribute(X "numBranchPoints").Value |> Int32.TryParse |> snd),
                                                                                                                     bv + (summary.Attribute(X "visitedBranchPoints").Value |> Int32.TryParse |> snd),
                                                                                                                     s + (summary.Attribute(X "numSequencePoints").Value |> Int32.TryParse |> snd),
                                                                                                                     sv + (summary.Attribute(X "visitedSequencePoints").Value |> Int32.TryParse |> snd))) (0,0,0,0)
                                                         let (b,bv,s,sv) = q
                                                         if s > 0 then cx.SetAttributeValue(X "line-rate", (float sv)/(float s))
                                                         if b > 0 then cx.SetAttributeValue(X "branch-rate", (float bv)/(float b))
                                                                                                                   )
    )

    extract (report.Descendants(X "CoverageSession") |> Seq.head) packages.Parent

  let Summary (report:XDocument) (format:Base.ReportFormat) result =
    let rewrite = XDocument(XDeclaration("1.0", "utf-8", "yes"), [||])
    let element = XElement(X "coverage",
                            XAttribute(X "line-rate", 0),
                            XAttribute(X "branch-rate", 0),
                            XAttribute(X "version", AssemblyVersionInformation.AssemblyVersion),
                            XAttribute(X "timestamp", int((DateTime.UtcNow - DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds))
                )

    rewrite.Add(element)
    let packages = XElement(X "packages")
    element.Add(packages)

    match format with
    | Base.ReportFormat.NCover -> NCover report packages
    | _ -> OpenCover report packages

    rewrite.Save(!path |> Option.get)
    result