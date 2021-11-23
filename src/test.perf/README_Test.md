# SqCore

Regular testing is required for keeping the source code healthy.
At least once per month (1st day of the month?), manual testing of the following is required:

 * [**Unit Tests**](#correctness) All libraries, tools, apps are tested in one big test project.
 * [**Performance Tests**](#speed,#benchmark) All libraries, tools, apps are benchmarked in one big test project.

Persisting history: the results of the tests (correctness, speed) are stored in a local folder which is archived too.
If the data is small, it can be stored in Redis or pSql.

In the future: the developer server will run these tests automatically every week and send email reports.
It sends special warning emails if the Benchmark test shows that a query of the webserver runs much slower than in the past.