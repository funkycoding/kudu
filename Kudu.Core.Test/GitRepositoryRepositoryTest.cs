﻿using Kudu.Core.SourceControl.Git;
using Xunit;
using System;
using System.Linq;
using Kudu.Core.SourceControl;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Test {
    public class GitRepositoryRepositoryTest {
        [Fact]
        public void IsDiffHeaderReturnsTrueForValidDiffHeaders() {
            Assert.True(GitExeRepository.IsDiffHeader("diff --git"));
        }

        [Fact]
        public void ConvertStatusUnknownStatusThrows() {
            Assert.Throws<InvalidOperationException>(() => GitExeRepository.ConvertStatus("AD"));
        }

        [Fact]
        public void ConvertStatusKnownStatuses() {
            ChangeType add = GitExeRepository.ConvertStatus("A");
            ChangeType addModified = GitExeRepository.ConvertStatus("AM");
            ChangeType modifiedAdd = GitExeRepository.ConvertStatus("M");
            ChangeType modifiedModified = GitExeRepository.ConvertStatus("MM");
            ChangeType deleted = GitExeRepository.ConvertStatus("D");
            ChangeType untracked = GitExeRepository.ConvertStatus("??");

            Assert.Equal(ChangeType.Added, add);
            Assert.Equal(ChangeType.Added, addModified);
            Assert.Equal(ChangeType.Modified, modifiedAdd);
            Assert.Equal(ChangeType.Modified, modifiedModified);
            Assert.Equal(ChangeType.Deleted, deleted);
            Assert.Equal(ChangeType.Untracked, untracked);
        }

        [Fact]
        public void ParseStatus() {
            var statusText = @"M  a
A  a.txt2
D  b
AM g.txt
MM a.txt
M  b.txt
?? x.txt
A  New File With Spaces.txt

";
            var status = GitExeRepository.ParseStatus(statusText.AsReader()).ToList();
            Assert.Equal(8, status.Count);

            Assert.Equal("a", status[0].Path);
            Assert.Equal(ChangeType.Modified, status[0].Status);

            Assert.Equal("a.txt2", status[1].Path);
            Assert.Equal(ChangeType.Added, status[1].Status);

            Assert.Equal("b", status[2].Path);
            Assert.Equal(ChangeType.Deleted, status[2].Status);

            Assert.Equal("g.txt", status[3].Path);
            Assert.Equal(ChangeType.Added, status[3].Status);

            Assert.Equal("a.txt", status[4].Path);
            Assert.Equal(ChangeType.Modified, status[4].Status);

            Assert.Equal("b.txt", status[5].Path);
            Assert.Equal(ChangeType.Modified, status[5].Status);

            Assert.Equal("x.txt", status[6].Path);
            Assert.Equal(ChangeType.Untracked, status[6].Status);

            Assert.Equal("New File With Spaces.txt", status[7].Path);
            Assert.Equal(ChangeType.Added, status[7].Status);
        }

        [Fact]
        public void PopulateStatusHandlesFilesWithSpaces() {
            string status = @"
A	New File
";
            ChangeSetDetail detail = new ChangeSetDetail();
            detail.Files["New File"] = new FileInfo();
            GitExeRepository.PopulateStatus(status.AsReader(), detail);

            Assert.Equal(ChangeType.Added, detail.Files["New File"].Status);
        }

        [Fact]
        public void ParseCommitParsesCommit() {
            string commitText = @"commit 307d8fe354ff30609decef49f91195e2e9719398
Author: David Fowler <davidfowl@gmail.com>
Date:   Thu Jul 7 19:05:40 2011 -0700

    Initial commit";

            ChangeSet changeSet = GitExeRepository.ParseCommit(commitText.AsReader());

            Assert.Equal("307d8fe354ff30609decef49f91195e2e9719398", changeSet.Id);
            Assert.Equal("David Fowler", changeSet.AuthorName);
            Assert.Equal("davidfowl@gmail.com", changeSet.AuthorEmail);
            Assert.Equal("    Initial commit", changeSet.Message);
        }

        [Fact]
        public void ParseCommitWithMultipleCommitsParsesOneCommit() {
            string commitText = @"commit d35697645e2472f5e327c0ec4b9f3489e806c276
Author: John Doe
Date:   Thu Jul 7 19:23:07 2011 -0700

    Second commit

commit 307d8fe354ff30609decef49f91195e2e9719398
Author: David Fowler <davidfowl@gmail.com>
Date:   Thu Jul 7 19:05:40 2011 -0700

    Initial commit
";

            ChangeSet changeSet = GitExeRepository.ParseCommit(commitText.AsReader());

            Assert.Equal("d35697645e2472f5e327c0ec4b9f3489e806c276", changeSet.Id);
            Assert.Equal("John Doe", changeSet.AuthorName);
            Assert.Null(changeSet.AuthorEmail);
            Assert.Equal(@"    Second commit
", changeSet.Message);
        }

        [Fact]
        public void Parse() {
            string summaryText = @"
1	1	NGitHub.nuspec
1	1	src/NGitHub/IRepositoryService.cs
5	5	src/NGitHub/RepositoryService.cs
1	1	src/NGitHub/SharedAssemblyInfo.cs
-	-	Test.dll
 4 files changed, 8 insertions(+), 8 deletions(-)
";

            var detail = new ChangeSetDetail();
            GitExeRepository.ParseSummary(summaryText.AsReader(), detail);

            Assert.Equal(5, detail.Files.Count);
            Assert.Equal(4, detail.FilesChanged);
            Assert.Equal(8, detail.Insertions);
            Assert.Equal(8, detail.Deletions);
            AssertFile(detail, "NGitHub.nuspec", insertions: 1, deletions: 1, binary: false);
            AssertFile(detail, "src/NGitHub/IRepositoryService.cs", insertions: 1, deletions: 1, binary: false);
            AssertFile(detail, "src/NGitHub/RepositoryService.cs", insertions: 5, deletions: 5, binary: false);
            AssertFile(detail, "src/NGitHub/SharedAssemblyInfo.cs", insertions: 1, deletions: 1, binary: false);
            AssertFile(detail, "Test.dll", binary: true);
        }

        [Fact]
        public void ParseDiffChunkHandlesFilesWithSpacesInName() {
            string diff = @"diff --git a/New File b/New File
new file mode 100644
index 0000000..261a6bf
--- /dev/null
+++ b/New File	
@@ -0,0 +1 @@
+Ayayayya
\ No newline at end of file";
            ChangeSetDetail detail = null;
            var diffChunk = GitExeRepository.ParseDiffChunk(diff.AsReader(), ref detail);
            Assert.False(diffChunk.Binary);
            Assert.Equal("New File", diffChunk.FileName);
            Assert.Equal(2, diffChunk.Lines.Count);
            Assert.Equal("+Ayayayya", diffChunk.Lines[1].Text.TrimEnd());
        }

        private void AssertFile(ChangeSetDetail detail, string path, int? insertions = null, int? deletions = null, bool binary = false) {
            FileInfo fi;
            Assert.True(detail.Files.TryGetValue(path, out fi));
            Assert.Equal(binary, fi.Binary);
            if (insertions != null) {
                Assert.Equal(insertions, fi.Insertions);
            }
            if (deletions != null) {
                Assert.Equal(deletions, fi.Deletions);
            }
        }
    }
}