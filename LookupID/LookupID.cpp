﻿#include <boost/regex.hpp>
#include <objbase.h>

#include <iostream>

enum class LookupMethod
{
    Regex,
    Exact,
    Contains,
    StartsWith,
    EndsWith
};


// Who would have thought that I would reference the quest port?
// this is my revenge Fern! (also pls dont bully me, ive never written c++ before)
extern "C" {
    _declspec(dllexport) void LookupID_internal(char* ppStrArray[], int count, int** ppArray, int* pSize, char* id, LookupMethod method)
    {
        std::function < bool(std::string_view) > predicate;
        boost::regex regex;

        try {
            switch (method) {
                case LookupMethod::Regex: {
                    regex = boost::regex(id, boost::regex_constants::ECMAScript | boost::regex_constants::optimize);
                    predicate = [&regex](const std::string_view n) {
                        return boost::regex_search(n.data(), regex);
                    };
                    break;
                }

                case LookupMethod::Exact: {
                    predicate = [id](const std::string_view n) { return n == id; };
                    break;
                }

                case LookupMethod::Contains: {
                    predicate = [id](const std::string_view n) {
                        return n.find(id) != std::string::npos;
                    };
                    break;
                }
                case LookupMethod::StartsWith: {
                    predicate = [id](const std::string_view n) {
                        return n.starts_with(id);
                    };
                    break;
                }

                case LookupMethod::EndsWith: {
                    predicate = [id](const std::string_view n) {
                        return n.ends_with(id);
                    };
                    break;
                }


                default: {
                    return;
                }
            }
        } catch(std::exception const& e) {
          const char* exception = e.what();
          std::cout << "Regex exception! \"" << exception << "\"" << std::endl;
          return;
        }

        // reduce allocations
        std::vector<int> ret;
        ret.reserve(count);

        for (int i = 0; i < count; i++)
        {
            char* str = ppStrArray[i];
            try {
                if (predicate(str))
                    ret.emplace_back(i);
            } catch(std::exception const& e) {
                const char* exception = e.what();
                std::cout << "Regex exception! \"" << exception << "\"" << std::endl;
                continue;
            }
        }

        size_t newSize = ret.size();
        *pSize = newSize;

        int* newArray = (int*)CoTaskMemAlloc(sizeof(int) * newSize);
        for (size_t i = 0; i != newSize; i++) {
            newArray[i] = ret[i];
        }

        *ppArray = newArray;


        /*for (size_t i = 0; i != newSize; i++) {
            *ppArray[i] = ret[i];
        }*/
    }
}
