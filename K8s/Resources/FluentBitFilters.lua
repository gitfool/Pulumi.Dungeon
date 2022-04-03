function transform(tag, timestamp, record)
    if record["kubernetes"] then
        record["Service"] = record["kubernetes"]["container_name"]
    elseif record["SYSLOG_IDENTIFIER"] then
        record["Service"] = record["SYSLOG_IDENTIFIER"]
    end

    if record["RenderedMessage"] then
        record["Message"], record["RenderedMessage"] = record["RenderedMessage"], nil
    elseif record["MESSAGE"] then
        record["Message"], record["MESSAGE"] = record["MESSAGE"], nil
    else
        record["Message"], record["log"] = record["log"], nil
    end

    if record["Properties"] and record["Properties"]["StatusCode"] then
        record["Properties"]["StatusCode"] = tostring(record["Properties"]["StatusCode"])
    end

    return 2, timestamp, record
end
