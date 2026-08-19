import re
# убираем строку "🤖 Generated with … Claude …"
message = re.sub(rb"(?im)^.*generated with.*claude.*\n?", b"", message)
# возвращаем сообщение без строки "Co-authored-by: … Claude …"
return re.sub(rb"(?im)^\s*co-authored-by:.*claude.*\n?", b"", message)
