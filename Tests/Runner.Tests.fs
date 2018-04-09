﻿namespace Tests.Runner

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Reflection
open System.Threading
open System.Xml
open System.Xml.Linq

open AltCover
open AltCover.Augment
open AltCover.Base
open Mono.Options
open NUnit.Framework

[<TestFixture>]
type AltCoverTests() = class

  // Base.fs

  [<Test>]
  member self.JunkUspidGivesNegativeIndex() =
    let key = " "
    let index = Counter.FindIndexFromUspid 0 key
    Assert.That (index, Is.LessThan 0)

  [<Test>]
  member self.RealIdShouldIncrementCount() =
    let visits = new Dictionary<string, Dictionary<int, int * Track list>>()
    let key = " "
    Counter.AddVisit visits key 23 Null
    Assert.That (visits.Count, Is.EqualTo 1)
    Assert.That (visits.[key].Count, Is.EqualTo 1)
    Assert.That (visits.[key].[23], Is.EqualTo (1,[]))

  [<Test>]
  member self.RealIdShouldIncrementList() =
    let visits = new Dictionary<string, Dictionary<int, int * Track list>>()
    let key = " "
    let payload = Time DateTime.UtcNow.Ticks
    Counter.AddVisit visits key 23 payload
    Assert.That (visits.Count, Is.EqualTo 1)
    Assert.That (visits.[key].Count, Is.EqualTo 1)
    Assert.That (visits.[key].[23], Is.EqualTo (0,[payload]))

  [<Test>]
  member self.DistinctIdShouldBeDistinct() =
    let visits = new Dictionary<string, Dictionary<int, int * Track list>>()
    let key = " "
    Counter.AddVisit visits key 23 Null
    Counter.AddVisit visits "key" 42 Null
    Assert.That (visits.Count, Is.EqualTo 2)

  [<Test>]
  member self.DistinctLineShouldBeDistinct() =
    let visits = new Dictionary<string, Dictionary<int, int * Track list>>()
    let key = " "
    Counter.AddVisit visits key 23 Null
    Counter.AddVisit visits key 42 Null
    Assert.That (visits.Count, Is.EqualTo 1)
    Assert.That (visits.[key].Count, Is.EqualTo 2)

  [<Test>]
  member self.RepeatVisitsShouldIncrementCount() =
    let visits = new Dictionary<string, Dictionary<int, int * Track list>>()
    let key = " "
    Counter.AddVisit visits key 23 Null
    Counter.AddVisit visits key 23 Null
    Assert.That (visits.[key].[23], Is.EqualTo (2, []))

  [<Test>]
  member self.RepeatVisitsShouldIncrementTotal() =
    let visits = new Dictionary<string, Dictionary<int, int * Track list>>()
    let key = " "
    let payload = Time DateTime.UtcNow.Ticks
    Counter.AddVisit visits key 23 Null
    Counter.AddVisit visits key 23 payload
    Assert.That (visits.[key].[23], Is.EqualTo (1, [payload]))

  member self.resource = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                         |> Seq.find (fun n -> n.EndsWith("SimpleCoverage.xml", StringComparison.Ordinal))
   member self.resource2 = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                          |> Seq.find (fun n -> n.EndsWith("Sample1WithOpenCover.xml", StringComparison.Ordinal))

  [<Test>]
  member self.KnownModuleWithPayloadMakesExpectedChangeInOpenCover() =
    Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource2)
    let size = int stream.Length
    let buffer = Array.create size 0uy
    Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
    use worker = new MemoryStream()
    worker.Write (buffer, 0, size)
    worker.Position <- 0L
    let payload = Dictionary<int,int * Track list>()
    [0..9 ]
    |> Seq.iter(fun i -> payload.[10 - i] <- (i+1, []))
    [11..12]
    |> Seq.iter(fun i -> payload.[i ||| Counter.BranchFlag] <- (i-10, []))
    let item = Dictionary<string, Dictionary<int, int * Track list>>()
    item.Add("7C-CD-66-29-A3-6C-6D-5F-A7-65-71-0E-22-7D-B2-61-B5-1F-65-9A", payload)
    Counter.UpdateReport ignore (fun _ _ -> ()) true item ReportFormat.OpenCover worker |> ignore
    worker.Position <- 0L
    let after = XmlDocument()
    after.Load worker
    Assert.That( after.SelectNodes("//SequencePoint")
                 |> Seq.cast<XmlElement>
                 |> Seq.map (fun x -> x.GetAttribute("vc")),
                 Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"])
    Assert.That( after.SelectNodes("//BranchPoint")
                 |> Seq.cast<XmlElement>
                 |> Seq.map (fun x -> x.GetAttribute("vc")),
                 Is.EquivalentTo [ "2"; "2"])

  [<Test>]
  member self.FlushLeavesExpectedTraces() =
    let saved = Console.Out
    let here = Directory.GetCurrentDirectory()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let reportFile = Path.Combine(unique, "FlushLeavesExpectedTraces.xml")
    try
      let visits = new Dictionary<string, Dictionary<int, int * Track list>>()
      use stdout = new StringWriter()
      Console.SetOut stdout
      Directory.CreateDirectory(unique) |> ignore
      Directory.SetCurrentDirectory(unique)

      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      do
        use worker = new FileStream(reportFile, FileMode.CreateNew)
        worker.Write(buffer, 0, size)
        ()

      let payload = Dictionary<int,int * Track list>()
      [0..9 ]
      |> Seq.iter(fun i -> payload.[i] <- (i+1, []))
      visits.["f6e3edb3-fb20-44b3-817d-f69d1a22fc2f"] <- payload

      Counter.DoFlush ignore (fun _ _ -> ()) true visits AltCover.Base.ReportFormat.NCover reportFile |> ignore

      use worker' = new FileStream(reportFile, FileMode.Open)
      let after = XmlDocument()
      after.Load worker'
      Assert.That( after.SelectNodes("//seqpnt")
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> x.GetAttribute("visitcount")),
                   Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"])
    finally
      if File.Exists reportFile then File.Delete reportFile
      Console.SetOut saved
      Directory.SetCurrentDirectory(here)
      try
        Directory.Delete(unique)
      with
      | :? IOException -> ()

  // Runner.fs and CommandLine.fs

  [<Test>]
  member self.UsageIsAsExpected() =
    let options = Runner.DeclareOptions ()
    let saved = Console.Error

    try
      use stderr = new StringWriter()
      Console.SetError stderr
      let empty = OptionSet()
      CommandLine.Usage ("UsageError", empty, options)
      let result = stderr.ToString().Replace("\r\n", "\n")
      let expected = """Error - usage is:
or
  Runner
  -r, --recorderDirectory=VALUE
                             The folder containing the instrumented code to
                               monitor (including the AltCover.Recorder.g.dll
                               generated by previous a use of the .net core
                               AltCover).
  -w, --workingDirectory=VALUE
                             Optional: The working directory for the
                               application launch
  -x, --executable=VALUE     The executable to run e.g. dotnet
      --collect              Optional: Process previously saved raw coverage
                               data, rather than launching a process.
  -l, --lcovReport=VALUE     Optional: File for lcov format version of the
                               collected data
  -?, --help, -h             Prints out the options.
"""

      Assert.That (result, Is.EqualTo (expected.Replace("\r\n", "\n")), "*" + result + "*")

    finally Console.SetError saved

  [<Test>]
  member self.ShouldLaunchWithExpectedOutput() =
    // Hack for running while instrumented
    let where = Assembly.GetExecutingAssembly().Location
    let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
#if NETCOREAPP2_0
    let path' = if Directory.Exists path then path
                else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
#else
    let path' = path
#endif
    let files = Directory.GetFiles(path')
    let program = files
                  |> Seq.filter (fun x -> x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                  |> Seq.head

    let saved = (Console.Out, Console.Error)
    let e0 = Console.Out.Encoding
    let e1 = Console.Error.Encoding
    try
      use stdout = { new StringWriter() with override self.Encoding with get() = e0 }
      use stderr = { new StringWriter() with override self.Encoding with get() = e1 }
      Console.SetOut stdout
      Console.SetError stderr

      let nonWindows = System.Environment.GetEnvironmentVariable("OS") <> "Windows_NT"
      let exe, args = if nonWindows then ("mono", "\"" + program + "\"") else (program, String.Empty)
      let r = CommandLine.Launch exe args (Path.GetDirectoryName (Assembly.GetExecutingAssembly().Location))
      Assert.That (r, Is.EqualTo 0)

      Assert.That(stderr.ToString(), Is.Empty)
      let result = stdout.ToString()
      let quote = if System.Environment.GetEnvironmentVariable("OS") = "Windows_NT" then "\"" else String.Empty
      let expected = "Command line : '" + quote + exe + quote + " " + args + "\'" + Environment.NewLine +
                     "Where is my rocket pack? " + Environment.NewLine

      // hack for Mono
      //let computed = if result.Length = 14 then
      //                 result |> Encoding.Unicode.GetBytes |> Array.takeWhile (fun c -> c <> 0uy)|> Encoding.UTF8.GetString
      //               else result

      //if "TRAVIS_JOB_NUMBER" |> Environment.GetEnvironmentVariable |> String.IsNullOrWhiteSpace || result.Length > 0 then
      Assert.That(result, Is.EqualTo(expected))
    finally
      Console.SetOut (fst saved)
      Console.SetError (snd saved)

  [<Test>]
  member self.ShouldHaveExpectedOptions() =
    let options = Runner.DeclareOptions ()
    Assert.That (options.Count, Is.EqualTo 7)
    Assert.That(options |> Seq.filter (fun x -> x.Prototype <> "<>")
                        |> Seq.forall (fun x -> (String.IsNullOrWhiteSpace >> not) x.Description))
    Assert.That (options |> Seq.filter (fun x -> x.Prototype = "<>") |> Seq.length, Is.EqualTo 1)

  [<Test>]
  member self.ParsingJunkIsAnError() =
    let options = Runner.DeclareOptions ()
    let parse = CommandLine.ParseCommandLine [| "/@thisIsNotAnOption" |] options
    match parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "UsageError")
                     Assert.That (y, Is.SameAs options)

  [<Test>]
  member self.ParsingJunkAfterSeparatorIsExpected() =
    let options = Runner.DeclareOptions ()
    let input = [| "--";  "/@thisIsNotAnOption"; "this should be OK" |]
    let parse = CommandLine.ParseCommandLine input options
    match parse with
    | Left _ -> Assert.Fail()
    | Right (x, y) -> Assert.That (x, Is.EquivalentTo (input |> Seq.skip 1))
                      Assert.That (y, Is.SameAs options)

  [<Test>]
  member self.ParsingHelpGivesHelp() =
    let options = Runner.DeclareOptions ()
    let input = [| "--?" |]
    let parse = CommandLine.ParseCommandLine input options
    match parse with
    | Left _ -> Assert.Fail()
    | Right (x, y) -> Assert.That (y, Is.SameAs options)

    match CommandLine.ProcessHelpOption parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "HelpText")
                     Assert.That (y, Is.SameAs options)

    // a "not sticky" test
    lock Runner.executable (fun () ->
      Runner.executable := None
      match CommandLine.ParseCommandLine [| "/x"; "x" |] options
            |> CommandLine.ProcessHelpOption with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty))

  [<Test>]
  member self.ParsingErrorHelpGivesHelp() =
    let options = Runner.DeclareOptions ()
    let input = [| "--o"; Path.GetInvalidPathChars() |> String |]
    let parse = CommandLine.ParseCommandLine input options
    match parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "UsageError")
                     Assert.That (y, Is.SameAs options)

    match CommandLine.ProcessHelpOption parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "UsageError")
                     Assert.That (y, Is.SameAs options)

    // a "not sticky" test
    lock Runner.executable (fun () ->
      Runner.executable := None
      match CommandLine.ParseCommandLine [| "/x"; "x" |] options
            |> CommandLine.ProcessHelpOption with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty))

  [<Test>]
  member self.ParsingExeGivesExe() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let unique = "some exe"
      let input = [| "-x"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      match !Runner.executable with
      | None -> Assert.Fail()
      | Some x -> Assert.That(Path.GetFileName x, Is.EqualTo unique)
    finally
      Runner.executable := None)

  [<Test>]
  member self.ParsingMultipleExeGivesFailure() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let unique = Guid.NewGuid().ToString()
      let input = [| "-x"; unique; "/x"; unique.Replace("-", "+") |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.executable := None)

  [<Test>]
  member self.ParsingNoExeGivesFailure() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let blank = " "
      let input = [| "-x"; blank; |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.executable := None)

  [<Test>]
  member self.ParsingWorkerGivesWorker() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Path.GetFullPath(".")
      let input = [| "-w"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      match Runner.workingDirectory with
      | None -> Assert.Fail()
      | Some x -> Assert.That(x, Is.EqualTo unique)
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingMultipleWorkerGivesFailure() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-w"; Path.GetFullPath("."); "/w"; Path.GetFullPath("..") |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingBadWorkerGivesFailure() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Guid.NewGuid().ToString().Replace("-", "*")
      let input = [| "-w"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingNoWorkerGivesFailure() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-w" |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingRecorderGivesRecorder() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Path.GetFullPath(".")
      let input = [| "-r"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      match Runner.recordingDirectory with
      | None -> Assert.Fail()
      | Some x -> Assert.That(x, Is.EqualTo unique)
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ParsingMultipleRecorderGivesFailure() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-r"; Path.GetFullPath("."); "/r"; Path.GetFullPath("..") |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ParsingBadRecorderGivesFailure() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Guid.NewGuid().ToString().Replace("-", "*")
      let input = [| "-r"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ParsingNoRecorderGivesFailure() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-r" |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ParsingCollectGivesCollect() =
    try
      Runner.collect <- false
      let options = Runner.DeclareOptions ()
      let input = [| "--collect" |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      Assert.That(Runner.collect, Is.True)
    finally
      Runner.collect <- false

  [<Test>]
  member self.ParsingMultipleCollectGivesFailure() =
    try
      Runner.collect <- false
      let options = Runner.DeclareOptions ()
      let input = [| "--collect"; "--collect" |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.collect <- false

  [<Test>]
  member self.ParsingLcovGivesLcove() =
    lock Runner.lcov (fun () ->
    try
      Runner.lcov := None
      Runner.Summaries <- [Runner.StandardSummary]
      let options = Runner.DeclareOptions ()
      let unique = "some exe"
      let input = [| "-l"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      match !Runner.lcov with
      | None -> Assert.Fail()
      | Some x -> Assert.That(Path.GetFileName x, Is.EqualTo unique)

      Assert.That (Runner.Summaries.Length, Is.EqualTo 2)
    finally
      Runner.Summaries <- [Runner.StandardSummary]
      Runner.lcov := None)

  [<Test>]
  member self.ParsingMultipleLcovGivesFailure() =
    lock Runner.lcov (fun () ->
    try
      Runner.lcov := None
      Runner.Summaries <- [Runner.StandardSummary]
      let options = Runner.DeclareOptions ()
      let unique = Guid.NewGuid().ToString()
      let input = [| "-l"; unique; "/l"; unique.Replace("-", "+") |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.Summaries <- [Runner.StandardSummary]
      Runner.lcov := None)

  [<Test>]
  member self.ParsingNoLcovGivesFailure() =
    lock Runner.lcov (fun () ->
    try
      Runner.lcov := None
      Runner.Summaries <- [Runner.StandardSummary]
      let options = Runner.DeclareOptions ()
      let blank = " "
      let input = [| "-l"; blank; |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.Summaries <- [Runner.StandardSummary]
      Runner.lcov := None)

  [<Test>]
  member self.ShouldRequireExe() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let parse = Runner.RequireExe (Right ([], options))
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.executable := None)

  [<Test>]
  member self.ShouldAcceptExe() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := Some "xxx"
      let options = Runner.DeclareOptions ()
      let parse = Runner.RequireExe (Right (["b"], options))
      match parse with
      | Right (x::y, z) -> Assert.That (z, Is.SameAs options)
                           Assert.That (x, Is.EqualTo "xxx")
                           Assert.That (y, Is.EquivalentTo ["b"])
      | _ -> Assert.Fail()
    finally
      Runner.executable := None)

  [<Test>]
  member self.ShouldRequireCollectIfNotExe() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      Runner.collect <- true
      let options = Runner.DeclareOptions ()
      let parse = Runner.RequireExe (Right (["a";"b"], options))
      match parse with
      | Right ([], z) -> Assert.That (z, Is.SameAs options)
      | _ -> Assert.Fail()
    finally
      Runner.collect <- false
      Runner.executable := None)

  [<Test>]
  member self.ShouldRejectExeIfCollect() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := Some "xxx"
      Runner.collect <- true
      let options = Runner.DeclareOptions ()
      let parse = Runner.RequireExe (Right (["b"], options))
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.collect <- false
      Runner.executable := None)

  [<Test>]
  member self.ShouldRequireWorker() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireWorker input
      match parse with
      | Right _ -> Assert.That(parse, Is.SameAs input)
                   Assert.That(Option.isSome Runner.workingDirectory)
      | _-> Assert.Fail()
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ShouldAcceptWorker() =
    try
      Runner.workingDirectory <- Some "ShouldAcceptWorker"
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireWorker input
      match parse with
      | Right _ -> Assert.That(parse, Is.SameAs input)
                   Assert.That(Runner.workingDirectory,
                               Is.EqualTo (Some "ShouldAcceptWorker"))
      | _-> Assert.Fail()
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ShouldRequireRecorder() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireRecorder input
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ShouldRequireRecorderDll() =
    try
      let where = Assembly.GetExecutingAssembly().Location
      let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
      let path' = if Directory.Exists path then path
                  else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
      Runner.recordingDirectory <- Some path'
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireRecorder input
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None
  [<Test>]
  member self.ShouldAcceptRecorder() =
    try
      let here = (Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName)
      let where = Path.Combine(here, Guid.NewGuid().ToString())
      Directory.CreateDirectory(where) |> ignore
      Runner.recordingDirectory <- Some where
      let create = Path.Combine(where, "AltCover.Recorder.g.dll")
      if create |> File.Exists |> not then do
        let from = Path.Combine(here, "AltCover.Recorder.dll")
        use frombytes = new FileStream(from, FileMode.Open, FileAccess.Read)
        use libstream = new FileStream(create, FileMode.Create)
        frombytes.CopyTo libstream

      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireRecorder input
      match parse with
      | Right _ -> Assert.That(parse, Is.SameAs input)
      | _-> Assert.Fail()
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ShouldProcessTrailingArguments() =
    // Hack for running while instrumented
    let where = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
#if NETCOREAPP2_0
    let path' = if Directory.Exists path then path
                else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
#else
    let path' = path
#endif
    let files = Directory.GetFiles(path')
    let program = files
                  |> Seq.filter (fun x -> x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                  |> Seq.head

    let saved = (Console.Out, Console.Error)
    let e0 = Console.Out.Encoding
    let e1 = Console.Error.Encoding
    try
      use stdout = { new StringWriter() with override self.Encoding with get() = e0 }
      use stderr = { new StringWriter() with override self.Encoding with get() = e1 }
      Console.SetOut stdout
      Console.SetError stderr

      let u1 = Guid.NewGuid().ToString()
      let u2 = Guid.NewGuid().ToString()

      let baseArgs= [program; u1; u2]
      let nonWindows = System.Environment.GetEnvironmentVariable("OS") <> "Windows_NT"
      let args = if nonWindows then "mono" :: baseArgs else baseArgs

      let r = CommandLine.ProcessTrailingArguments args <| DirectoryInfo(where)
      Assert.That(r, Is.EqualTo 0)

      Assert.That(stderr.ToString(), Is.Empty)
      stdout.Flush()
      let result = stdout.ToString()
      let quote = if System.Environment.GetEnvironmentVariable("OS") = "Windows_NT" then "\"" else String.Empty
      let expected = "Command line : '" + quote + args.Head + quote + " " + String.Join(" ", args.Tail) +
                     "'" + Environment.NewLine + "Where is my rocket pack? " +
                     u1 + "*" + u2 + Environment.NewLine

      // hack for Mono
      //let computed = if result.Length = 50 then
      //                 result |> Encoding.Unicode.GetBytes |> Array.takeWhile (fun c -> c <> 0uy)|> Encoding.UTF8.GetString
      //               else result
      //if "TRAVIS_JOB_NUMBER" |> Environment.GetEnvironmentVariable |> String.IsNullOrWhiteSpace || result.Length > 0 then
      Assert.That(result, Is.EqualTo expected)
    finally
      Console.SetOut (fst saved)
      Console.SetError (snd saved)

  [<Test>]
  member self.ShouldNoOp() =
    let where = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let r = CommandLine.ProcessTrailingArguments [] <| DirectoryInfo(where)
    Assert.That(r, Is.EqualTo 0)

  [<Test>]
  member self.ErrorResponseIsAsExpected() =
    let saved = Console.Error
    try
      use stderr = new StringWriter()
      Console.SetError stderr
      let unique = Guid.NewGuid().ToString()
      let main = typeof<Tracer>.Assembly.GetType("AltCover.AltCover").GetMethod("Main", BindingFlags.NonPublic ||| BindingFlags.Static)
      let returnCode = main.Invoke(null, [| [| "RuNN"; "-r"; unique |] |])
      Assert.That(returnCode, Is.EqualTo 255)
      let result = stderr.ToString().Replace("\r\n", "\n")
      let expected = "\"RuNN\" \"-r\" \"" + unique + "\"\n" +
                     "--recorderDirectory : Directory " + unique + " not found\n" +
                       """Error - usage is:
  -i, --inputDirectory=VALUE Optional: The folder containing assemblies to
                               instrument (default: current directory)
  -o, --outputDirectory=VALUE
                             Optional: The folder to receive the instrumented
                               assemblies and their companions (default: sub-
                               folder '__Instrumented' of the current directory;
                                or '__Saved' if 'inplace' is set)
  -y, --symbolDirectory=VALUE
                             Optional, multiple: Additional directory to search
                               for matching symbols for the assemblies in the
                               input directory
"""
#if NETCOREAPP2_0
#else
                     + """  -k, --key=VALUE            Optional, multiple: any other strong-name key to
                               use
      --sn, --strongNameKey=VALUE
                             Optional: The default strong naming key to apply
                               to instrumented assemblies (default: None)
"""
#endif
                     + """  -x, --xmlReport=VALUE      Optional: The output report template file (default:
                                coverage.xml in the current directory)
  -f, --fileFilter=VALUE     Optional: source file name to exclude from
                               instrumentation (may repeat)
  -s, --assemblyFilter=VALUE Optional: assembly name to exclude from
                               instrumentation (may repeat)
  -e, --assemblyExcludeFilter=VALUE
                             Optional: assembly which links other instrumented
                               assemblies but for which internal details may be
                               excluded (may repeat)
  -t, --typeFilter=VALUE     Optional: type name to exclude from
                               instrumentation (may repeat)
  -m, --methodFilter=VALUE   Optional: method name to exclude from
                               instrumentation (may repeat)
  -a, --attributeFilter=VALUE
                             Optional: attribute name to exclude from
                               instrumentation (may repeat)
  -c, --callContext=VALUE    Optional, multiple: Tracking either times of
                               visits in ticks or designated method calls
                               leading to the visits.
                                   A single digit 0-7 gives the number of
                               decimal places of seconds to report; everything
                               else is at the mercy of the system clock
                               information available through DateTime.UtcNow
                                   A string in brackets "[]" is interpreted as
                               an attribute type name (the trailing "Attribute"
                               is optional), so [Test] or [TestAttribute] will
                               match; if the name contains one or more ".",
                               then it will be matched against the full name of
                               the attribute type.
                                   Other strings are interpreted as method
                               names (fully qualified if the string contains
                               any "." characters).
      --opencover            Optional: Generate the report in OpenCover format
      --inplace              Optional: Instrument the inputDirectory, rather
                               than the outputDirectory (e.g. for dotnet test)
      --save                 Optional: Write raw coverage data to file for
                               later processing
  -?, --help, -h             Prints out the options.
or
  Runner
  -r, --recorderDirectory=VALUE
                             The folder containing the instrumented code to
                               monitor (including the AltCover.Recorder.g.dll
                               generated by previous a use of the .net core
                               AltCover).
  -w, --workingDirectory=VALUE
                             Optional: The working directory for the
                               application launch
  -x, --executable=VALUE     The executable to run e.g. dotnet
      --collect              Optional: Process previously saved raw coverage
                               data, rather than launching a process.
  -l, --lcovReport=VALUE     Optional: File for lcov format version of the
                               collected data
  -?, --help, -h             Prints out the options.
"""

      Assert.That (result.Replace("\r\n", "\n"), Is.EqualTo (expected.Replace("\r\n", "\n")))

    finally Console.SetError saved

  [<Test>]
  member self.ShouldGetStringConstants() =
    let where = Assembly.GetExecutingAssembly().Location
                |> Path.GetDirectoryName
    let save = Runner.RecorderName
    lock self (fun () ->
    try
      Runner.recordingDirectory <- Some where
      Runner.RecorderName <- "AltCover.Recorder.dll"
      let instance = Runner.RecorderInstance()
      Assert.That(instance.FullName, Is.EqualTo "AltCover.Recorder.Instance", "should be the instance")
      let token = (Runner.GetMethod instance "get_Token") |> Runner.GetFirstOperandAsString
      Assert.That(token, Is.EqualTo "AltCover", "should be plain token")
      let report = (Runner.GetMethod instance "get_ReportFile") |> Runner.GetFirstOperandAsString
      Assert.That(report, Is.EqualTo "Coverage.Default.xml", "should be default coverage file")

    finally
      Runner.recordingDirectory <- None
      Runner.RecorderName <- save)

  [<Test>]
  member self.ShouldProcessPayload() =
    // Hack for running while instrumented
    let where = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
#if NETCOREAPP2_0
    let path' = if Directory.Exists path then path
                else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
#else
    let path' = path
#endif
    let files = Directory.GetFiles(path')
    let program = files
                  |> Seq.filter (fun x -> x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                  |> Seq.head

    let saved = (Console.Out, Console.Error)
    Runner.workingDirectory <- Some where
    let e0 = Console.Out.Encoding
    let e1 = Console.Error.Encoding
    try
      use stdout = { new StringWriter() with override self.Encoding with get() = e0 }
      use stderr = { new StringWriter() with override self.Encoding with get() = e1 }
      Console.SetOut stdout
      Console.SetError stderr

      let u1 = Guid.NewGuid().ToString()
      let u2 = Guid.NewGuid().ToString()
      use latch = new ManualResetEvent true

      let baseArgs= [program; u1; u2]
      let nonWindows = System.Environment.GetEnvironmentVariable("OS") <> "Windows_NT"
      let args = if nonWindows then "mono" :: baseArgs else baseArgs
      let r = Runner.GetPayload args
      Assert.That(r, Is.EqualTo 0)

      Assert.That(stderr.ToString(), Is.Empty)
      stdout.Flush()
      let result = stdout.ToString()
      let quote = if System.Environment.GetEnvironmentVariable("OS") = "Windows_NT" then "\"" else String.Empty
      let expected = "Command line : '" + quote + args.Head + quote + " " + String.Join(" ", args.Tail) +
                     "'" + Environment.NewLine + "Where is my rocket pack? " +
                     u1 + "*" + u2 + Environment.NewLine

      // hack for Mono
      //let computed = if result.Length = 50 then
      //                 result |> Encoding.Unicode.GetBytes |> Array.takeWhile (fun c -> c <> 0uy)|> Encoding.UTF8.GetString
      //               else result
      //if "TRAVIS_JOB_NUMBER" |> Environment.GetEnvironmentVariable |> String.IsNullOrWhiteSpace || result.Length > 0 then
      Assert.That(result, Is.EqualTo expected)
    finally
      Console.SetOut (fst saved)
      Console.SetError (snd saved)
      Runner.workingDirectory <- None

  [<Test>]
  member self.ShouldDoCoverage() =
    let start = Directory.GetCurrentDirectory()
    let here = (Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName)
    let where = Path.Combine(here, Guid.NewGuid().ToString())
    Directory.CreateDirectory(where) |> ignore
    Directory.SetCurrentDirectory where
    let create = Path.Combine(where, "AltCover.Recorder.g.dll")
    if create |> File.Exists |> not then do
        let from = Path.Combine(here, "AltCover.Recorder.dll")
        let updated = Instrument.PrepareAssembly from
        Instrument.WriteAssembly updated create

    let save = Runner.RecorderName
    let save1 = Runner.GetPayload
    let save2 = Runner.GetMonitor
    let save3 = Runner.DoReport

    let report =  "coverage.xml" |> Path.GetFullPath
    try
      Runner.RecorderName <- "AltCover.Recorder.g.dll"
      let payload (rest:string list) =
        Assert.That(rest, Is.EquivalentTo [|"test"; "1"|])
        255

      let monitor (hits:ICollection<(string*int*Base.Track)>) (token:string) _ _ =
        Assert.That(token, Is.EqualTo report, "should be default coverage file")
        Assert.That(hits, Is.Empty)
        127

      let write (hits:ICollection<(string*int*Base.Track)>) format (report:string) =
        Assert.That(report, Is.EqualTo report, "should be default coverage file")
        Assert.That(hits, Is.Empty)
        TimeSpan.Zero

      Runner.GetPayload <- payload
      Runner.GetMonitor <- monitor
      Runner.DoReport <- write

      let empty = OptionSet()
      let dummy = report + ".xx.acv"
      do
        use temp = File.Create dummy
        dummy |> File.Exists |> Assert.That

      let r = Runner.DoCoverage [|"Runner"; "-x"; "test"; "-r"; where; "--"; "1"|] empty
      dummy |> File.Exists |> not |> Assert.That
      Assert.That (r, Is.EqualTo 127)

    finally
      Runner.GetPayload <- save1
      Runner.GetMonitor <- save2
      Runner.DoReport <- save3
      Runner.RecorderName <- save
      Directory.SetCurrentDirectory start

  [<Test>]
  member self.WriteLeavesExpectedTraces() =
    let saved = Console.Out
    let here = Directory.GetCurrentDirectory()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let reportFile = Path.Combine(unique, "FlushLeavesExpectedTraces.xml")
    try
      let visits = new Dictionary<string, Dictionary<int, int>>()
      use stdout = new StringWriter()
      Console.SetOut stdout
      Directory.CreateDirectory(unique) |> ignore
      Directory.SetCurrentDirectory(unique)

      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      do
        use worker = new FileStream(reportFile, FileMode.CreateNew)
        worker.Write(buffer, 0, size)
        ()

      let hits = List<(string*int*Base.Track)>()
      [0..9 ]
      |> Seq.iter(fun i ->
        for j = 1 to i+1 do
          hits.Add("f6e3edb3-fb20-44b3-817d-f69d1a22fc2f", i, Base.Null)
          ignore j
      )

      let payload = Dictionary<int,int>()
      [0..9 ]
      |> Seq.iter(fun i -> payload.[i] <- (i+1))
      visits.["f6e3edb3-fb20-44b3-817d-f69d1a22fc2f"] <- payload

      Runner.DoReport hits AltCover.Base.ReportFormat.NCover reportFile |> ignore

      use worker' = new FileStream(reportFile, FileMode.Open)
      let after = XmlDocument()
      after.Load worker'
      Assert.That( after.SelectNodes("//seqpnt")
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> x.GetAttribute("visitcount")),
                   Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"])
    finally
      if File.Exists reportFile then File.Delete reportFile
      Console.SetOut saved
      Directory.SetCurrentDirectory(here)
      try
        Directory.Delete(unique)
      with
      | :? IOException -> ()

  [<Test>]
  member self.NullPayloadShouldReportNothing() =
    let hits = List<string*int*Base.Track>()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    do
      use s = File.Create (unique + ".0.acv")
      s.Close()
    let r = Runner.GetMonitor hits unique List.length []
    Assert.That(r, Is.EqualTo 0)
    Assert.That (File.Exists (unique + ".acv"))
    Assert.That(hits, Is.Empty)

  [<Test>]
  member self.ActivePayloadShouldReportAsExpected() =
    let hits = List<string*int*Base.Track>()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let formatter = System.Runtime.Serialization.Formatters.Binary.BinaryFormatter()
    let r = Runner.GetMonitor hits unique (fun l ->
       use sink = new DeflateStream(File.OpenWrite (unique + ".0.acv"), CompressionMode.Compress)
       l |> List.mapi (fun i x -> formatter.Serialize(sink, (x,i)); x) |> List.length
                                           ) ["a"; "b"; String.Empty; "c"]
    Assert.That(r, Is.EqualTo 4)
    Assert.That (File.Exists (unique + ".acv"))
    Assert.That(hits, Is.EquivalentTo [("a",0,Base.Null); ("b",1,Base.Null)])

  [<Test>]
  member self.JunkPayloadShouldReportAsExpected() =
    let hits = List<string*int*Base.Track>()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let formatter = System.Runtime.Serialization.Formatters.Binary.BinaryFormatter()
    let r = Runner.GetMonitor hits unique (fun l ->
       use sink = new DeflateStream(File.OpenWrite (unique + ".0.acv"), CompressionMode.Compress)
       l |> List.mapi (fun i x -> formatter.Serialize(sink, (x,i,Base.Null,DateTime.UtcNow)); x) |> List.length
                                           ) ["a"; "b"; String.Empty; "c"]
    Assert.That(r, Is.EqualTo 4)
    Assert.That (File.Exists (unique + ".acv"))
    Assert.That(hits, Is.EquivalentTo [])

  [<Test>]
  member self.TrackingPayloadShouldReportAsExpected() =
    let hits = List<string*int*Base.Track>()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let formatter = System.Runtime.Serialization.Formatters.Binary.BinaryFormatter()
    let payloads = [Base.Null
                    Base.Call 17
                    Base.Time 23L
                    Base.Both (5L, 42)
                    Base.Time 42L
                    Base.Call 5]
    let inputs = [
                   "a"
                   "b"
                   "c"
                   "d"
                   String.Empty
                   "e"
                 ]
    let r = Runner.GetMonitor hits unique (fun l ->
       use sink = new DeflateStream(File.OpenWrite (unique + ".0.acv"), CompressionMode.Compress)
       l |> List.zip payloads
       |> List.mapi (fun i (y,x) -> formatter.Serialize(sink, (x,i,y)); x) |> List.length) inputs

    let expected = inputs |> List.zip payloads
                   |> List.mapi (fun i (y,x) -> (x,i,y))
                   |> List.take 4
    Assert.That(r, Is.EqualTo 6)
    Assert.That (File.Exists (unique + ".acv"))
    Assert.That(hits, Is.EquivalentTo expected)

  [<Test>]
  member self.PointProcessShouldCaptureTimes() =
    let x = XmlDocument()
    x.LoadXml("<root />")
    let root = x.DocumentElement
    let hits = [Base.Null
                Base.Call 17
                Base.Time 23L
                Base.Both (5L, 42)
                Base.Time 42L
                Base.Time 5L]
    Runner.PointProcess root hits

    Assert.That(x.DocumentElement.OuterXml,
                Is.EqualTo """<root><Times><Time time="5" vc="2" /><Time time="23" vc="1" /><Time time="42" vc="1" /></Times><TrackedMethodRefs><TrackedMethodRef uid="17" vc="1" /><TrackedMethodRef uid="42" vc="1" /></TrackedMethodRefs></root>""")

  [<Test>]
  member self.PostprocessShouldRestoreKnownOpenCoverState() =
    Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource2)
    let after = XmlDocument()
    after.Load stream
    let before = after.OuterXml

    after.DocumentElement.SelectNodes("//Summary")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("visitedBranchPoints", "0")
                          el.SetAttribute("branchCoverage", "0")
                          el.SetAttribute("visitedSequencePoints", "0")
                          el.SetAttribute("sequenceCoverage", "0")
                          el.SetAttribute("visitedClasses", "0")
                          el.SetAttribute("visitedMethods", "0")
                           )

    after.DocumentElement.SelectNodes("//Method")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("visited", "false")
                          el.SetAttribute("sequenceCoverage", "0")
                          el.SetAttribute("branchCoverage", "0")
                           )

    after.DocumentElement.SelectNodes("//SequencePoint")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("bev", "0")
                           )

    let empty = Dictionary<string, Dictionary<int, int * Track list>>()
    Runner.PostProcess empty Base.ReportFormat.OpenCover after

    Assert.That(after.OuterXml.Replace("uspid=\"100663298", "uspid=\"13"), Is.EqualTo before, after.OuterXml)

  [<Test>]
  member self.PostprocessShouldRestoreKnownOpenCoverStateFromMono() =
    Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
    let resource = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                         |> Seq.find (fun n -> n.EndsWith("HandRolledMonoCoverage.xml", StringComparison.Ordinal))

    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
    let after = XmlDocument()
    after.Load stream
    let before = after.OuterXml

    after.DocumentElement.SelectNodes("//Summary")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("visitedBranchPoints", "0")
                          el.SetAttribute("branchCoverage", "0")
                          el.SetAttribute("visitedSequencePoints", "0")
                          el.SetAttribute("sequenceCoverage", "0")
                          el.SetAttribute("visitedClasses", "0")
                          el.SetAttribute("visitedMethods", "0")
                           )

    after.DocumentElement.SelectNodes("//Method")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("visited", "false")
                          el.SetAttribute("sequenceCoverage", "0")
                          el.SetAttribute("branchCoverage", "0")
                           )

    after.DocumentElement.SelectNodes("//SequencePoint")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("bev", "0")
                           )

    after.DocumentElement.SelectNodes("//MethodPoint")
    |> Seq.cast<XmlElement>
    |> Seq.toList
    |> List.iter(fun el -> el.RemoveAllAttributes())

    let visits = Dictionary<string, Dictionary<int, int * Track list>>()
    let visit = Dictionary<int, int * Track list>()
    visits.Add("6A-33-AA-93-82-ED-22-9D-F8-68-2C-39-5B-93-9F-74-01-76-00-9F", visit)
    visit.Add(100663297, (1,[]))  // should fill in the expected non-zero value
    visit.Add(100663298, (23,[])) // should be ignored
    Runner.PostProcess visits Base.ReportFormat.OpenCover after

    Assert.That(after.OuterXml.Replace("uspid=\"100663298", "uspid=\"13"), Is.EqualTo before, after.OuterXml)

  [<Test>]
  member self.PostprocessShouldRestoreDegenerateOpenCoverState() =
    Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource2)
    let after = XmlDocument()
    after.Load stream

    after.DocumentElement.SelectNodes("//Summary")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("visitedBranchPoints", "0")
                          el.SetAttribute("branchCoverage", "0")
                          el.SetAttribute("visitedSequencePoints", "0")
                          el.SetAttribute("sequenceCoverage", "0")
                          el.SetAttribute("visitedClasses", "0")
                          el.SetAttribute("visitedMethods", "0")
                           )

    after.DocumentElement.SelectNodes("//Method")
    |> Seq.cast<XmlElement>
    |> Seq.iter(fun el -> el.SetAttribute("visited", "false")
                          el.SetAttribute("sequenceCoverage", "0")
                          el.SetAttribute("branchCoverage", "0")
                           )

    after.DocumentElement.SelectNodes("//SequencePoint")
    |> Seq.cast<XmlElement>
    |> Seq.toList
    |> List.iter(fun el -> el |> el.ParentNode.RemoveChild |> ignore)

    after.DocumentElement.SelectNodes("//MethodPoint")
    |> Seq.cast<XmlElement>
    |> Seq.toList
    |> List.iter(fun el -> el |> el.ParentNode.RemoveChild |> ignore)

    let before = after.OuterXml.Replace("uspid=\"13", "uspid=\"100663298")

    let empty = Dictionary<string, Dictionary<int, int * Track list>>()
    Runner.PostProcess empty Base.ReportFormat.OpenCover after

    Assert.That(after.OuterXml, Is.EqualTo before, after.OuterXml)

  [<Test>]
  member self.JunkTokenShouldDefaultZero() =
    let visits = Dictionary<int, int * Track list>()
    let key = " "
    let result = Runner.LookUpVisitsByToken key visits
    match result with
    | (0, []) -> ()
    | _ -> Assert.Fail(sprintf "%A" result)

  [<Test>]
  member self.OpenCoverShouldGeneratePlausibleLcov() =
    let resource = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                        |> Seq.find (fun n -> n.EndsWith("Sample1WithOpenCover.xml", StringComparison.Ordinal))

    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)

    let baseline = XDocument.Load(stream)
    let unique = Path.Combine(Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName,
                                Guid.NewGuid().ToString() + "/OpenCover.lcov")
    Runner.lcov := Some unique
    unique |> Path.GetDirectoryName |>  Directory.CreateDirectory |> ignore

    try
      Runner.LCovSummary baseline Base.ReportFormat.OpenCover

      let result = File.ReadAllText unique

      let resource2 = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                        |> Seq.find (fun n -> n.EndsWith("OpenCover.lcov", StringComparison.Ordinal))

      use stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource2)
      use reader = new StreamReader(stream2)
      let expected = reader.ReadToEnd().Replace("\r\n", Environment.NewLine)
      Assert.That (result, Is.EqualTo expected)
    finally
      Runner.lcov := None

  [<Test>]
  member self.NCoverShouldGeneratePlausibleLcov() =
    let resource = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                        |> Seq.find (fun n -> n.EndsWith("SimpleCoverage.xml", StringComparison.Ordinal))

    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)

    let baseline = XDocument.Load(stream)
    let unique = Path.Combine(Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName,
                                Guid.NewGuid().ToString() + "/NCover.lcov")
    Runner.lcov := Some unique
    unique |> Path.GetDirectoryName |>  Directory.CreateDirectory |> ignore

    try
      Runner.LCovSummary baseline Base.ReportFormat.NCover

      let result = File.ReadAllText unique

      let resource2 = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                        |> Seq.find (fun n -> n.EndsWith("NCover.lcov", StringComparison.Ordinal))

      use stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource2)
      use reader = new StreamReader(stream2)
      let expected = reader.ReadToEnd().Replace("\r\n", Environment.NewLine)
      Assert.That (result, Is.EqualTo expected)
    finally
      Runner.lcov := None

end