using ExamQuestion.Models;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ExamQuestion.Utils
{
    public class AssignmentHandler
    {
        internal static async Task<MemoryStream> CreateZipArchive(IEnumerable<Document> documents, string studentName)
        {
            await using var ms = new MemoryStream();
            using var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

            foreach (var document in documents)
            {
                var fileBytes = await downloadFile(document.Url);

                if (fileBytes != null)
                {
                    var zipFileName = document.PublicFileName;
                    if (document.Url.Contains("1drv.ms"))
                        zipFileName = $"{Path.GetFileNameWithoutExtension(document.PublicFileName)}.html";
                    var zipArchiveEntry = archive.CreateEntry(zipFileName, CompressionLevel.Fastest);

                    await using var zipStream = zipArchiveEntry.Open();
                    await zipStream.WriteAsync(fileBytes, offset: 0, fileBytes.Length);
                }
            }

            return ms;
        }

        private static async Task<byte[]> downloadFile(string url)
        {
            using var client = new HttpClient();
            using var result = await client.GetAsync(url);

            if (result.IsSuccessStatusCode)
                return await result.Content.ReadAsByteArrayAsync();

            return null;
        }

        internal static async Task<List<Document>> GetStudentDocuments(AppDbContext db, Student student, Exam exam,
            string ip, Random r) =>
            CalcStudentDocuments(await collectAllocationInfo(db, student, exam), ip, r);

        private static async Task<AllocationInfo> collectAllocationInfo(AppDbContext db, Student student, Exam exam)
        {
            var ai = new AllocationInfo
            {
                StudentCount = await db.Students.CountAsync(s => s.CourseId == student.CourseId)
            };

            var questions = await db.Questions.Where(q => q.ExamId == exam.Id).ToListAsync();
            foreach (var question in questions)
            {
                var docs = await db.Documents.Where(d => d.QuestionId == question.Id).ToListAsync();
                //get the documents for this question
                ai.Documents.Add(docs);

                foreach (var doc in docs)
                    ai.Assignments.Add(doc.Id, (await db.Assignments.Where(a => a.DocumentId == doc.Id).ToListAsync()).Distinct().ToList());
            }

            return ai;
        }

        //public so we can run tests
        public static List<Document> CalcStudentDocuments(AllocationInfo ai, string ip, Random r)
        {
            var assignments = new List<Document>();

            //for each question in this exam
            foreach (var docs in ai.Documents.Where(d => d.Count > 0))
            {
                //how many times should we repeat each
                var repeatCount = (int)Math.Ceiling((float)ai.StudentCount / docs.Count);
                var repeatCounts = Enumerable.Repeat(repeatCount, docs.Count).ToList();

                //reduce that by the number of times each has already been assigned
                for (var i = 0; i < docs.Count; i++)
                    if (ai.Assignments.TryGetValue(docs[i].Id, out var curAssignments))
                        repeatCounts[i] -= curAssignments.Count;

                //reduce to zero if someone at the same IP address has already been assigned this document
                var ipRepeatCounts = new List<int>();
                for (var i = 0; i < docs.Count; i++)
                {
                    ipRepeatCounts.Add(repeatCounts[i]);
                    if (ai.Assignments.TryGetValue(docs[i].Id, out var curAssignments))
                        if (curAssignments.Any(a => a.Ip == ip))
                            ipRepeatCounts[i] = 0;
                }

                //if this hasn't removed all possibilities, then take the more restrictive option
                //if everyone is in the same classroom (and thus same IP) this ensures that there are
                //still items to distribute
                if (ipRepeatCounts.Sum() > 0)
                    repeatCounts = ipRepeatCounts;

                //create a pool of unassigned documents
                var docPool = new List<Document>();
                for (var i = 0; i < repeatCounts.Count; i++)
                for (var j = 0; j < repeatCounts[i]; j++)
                    docPool.Add(docs[i]);

                //assign a random document from the remaining pool to the student
                assignments.Add(docPool[r.Next(docPool.Count)]);
            }

            return assignments;
        }

        public class AllocationInfo
        {
            public int StudentCount { get; set; }
            public List<List<Document>> Documents { get; } = new List<List<Document>>();
            public Dictionary<int, List<Assignment>> Assignments { get; } = new Dictionary<int, List<Assignment>>();
        }
    }
}