using ExamQuestion.Models;
using ExamQuestion.Utils;
using System;
using System.Collections.Generic;
using Xunit;

namespace ExamQuestion.Test
{
    public class CalcStudentDocuments
    {
        // no documents to assign
        [Fact]
        public void NoDocuments()
        {
            var ai = new AssignmentHandler.AllocationInfo();

            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random());

            Assert.True(docs.Count == 0);
        }

        [Fact]
        public void NoDocument_ListsEmpty()
        {
            var ai = new AssignmentHandler.AllocationInfo { StudentCount = 1 };
            ai.Documents.Add(new List<Document> ());
            ai.Assignments.Add(1, new List<Assignment>());
            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random());

            Assert.True(docs.Count == 0);
        }

        //one question, one document to assign to one student
        [Fact]
        public void OneDocument()
        {
            var ai = new AssignmentHandler.AllocationInfo {StudentCount = 1};
            ai.Documents.Add(new List<Document> {new Document {Id = 1}});

            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random());

            Assert.True(docs.Count == 1);
            Assert.True(docs[0].Id == 1);
        }

        //one question, two students, one already has a document, assign the other to the second student
        [Fact]
        public void OneDocumentLeft()
        {
            var ai = new AssignmentHandler.AllocationInfo {StudentCount = 2};
            ai.Documents.Add(new List<Document> {new Document {Id = 1}, new Document {Id = 2}});
            ai.Assignments.Add(1, new List<Assignment> {new Assignment {DocumentId = 1, Ip = "0.0.0.1"}});

            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random());

            Assert.True(docs.Count == 1);
            Assert.True(docs[0].Id == 2);
        }

        //one question, two documents, three students at same IP, two already have a document.
        [Fact]
        public void NoDocumentsLeft_SameIP()
        {
            var ai = new AssignmentHandler.AllocationInfo { StudentCount = 3 };
            ai.Documents.Add(new List<Document> { new Document { Id = 1 }, new Document { Id = 2 } });
            ai.Assignments.Add(1, new List<Assignment> { new Assignment { DocumentId = 1, Ip = "0.0.0.0" } });
            ai.Assignments.Add(2, new List<Assignment> { new Assignment { DocumentId = 2, Ip = "0.0.0.0" } });

            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random(1));

            Assert.True(docs.Count == 1);
            Assert.True(docs[0].Id == 1);
        }

        //two questions, one document each, assign to student
        [Fact]
        public void TwoQuestions()
        {
            var ai = new AssignmentHandler.AllocationInfo {StudentCount = 3};
            ai.Documents.AddRange(new List<List<Document>>
            {
                new List<Document> {new Document {Id = 1}}, new List<Document> {new Document {Id = 3}},
            });

            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random());

            Assert.True(docs.Count == 2);
            Assert.True(docs[0].Id == 1);
            Assert.True(docs[1].Id == 3);
        }

        //two questions, one document each, assign to student
        [Fact]
        public void TwoQuestions_ManyDocuments()
        {
            var ai = new AssignmentHandler.AllocationInfo {StudentCount = 18};
            ai.Documents.AddRange(new List<List<Document>>
            {
                new List<Document>
                {
                    new Document {Id = 1},
                    new Document {Id = 3},
                    new Document {Id = 5},
                    new Document {Id = 7},
                    new Document {Id = 9},
                    new Document {Id = 11}
                },
                new List<Document>
                {
                    new Document {Id = 2},
                    new Document {Id = 4},
                    new Document {Id = 6},
                    new Document {Id = 8},
                    new Document {Id = 10},
                    new Document {Id = 12}
                },
            });

            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random(1));

            Assert.True(docs.Count == 2);
            Assert.True(docs[0].Id == 3);
            Assert.True(docs[1].Id == 2);
        }

        //two questions, one document each, assign to student
        [Fact]
        public void TwoQuestions_SameIP()
        {
            var ai = new AssignmentHandler.AllocationInfo { StudentCount = 25 };
            ai.Documents.AddRange(new List<List<Document>>
            {
                new List<Document>
                {
                    new Document {Id = 1},
                    new Document {Id = 3},
                    new Document {Id = 5},
                    new Document {Id = 7},
                    new Document {Id = 9},
                    new Document {Id = 11}
                },
                new List<Document>
                {
                    new Document {Id = 2},
                    new Document {Id = 4},
                    new Document {Id = 6},
                    new Document {Id = 8},
                    new Document {Id = 10},
                    new Document {Id = 12}
                },
            });
            ai.Assignments.Add(2, new List<Assignment> { new Assignment { DocumentId = 2, Ip = "0.0.0.0" } });

            var docs = AssignmentHandler.CalcStudentDocuments(ai, "0.0.0.0", new Random(1));

            //doc 2 is already taken at this IP, so 4 is assigned instead
            Assert.True(docs.Count == 2);
            Assert.True(docs[0].Id == 3);
            Assert.True(docs[1].Id == 4);
        }
    }
}