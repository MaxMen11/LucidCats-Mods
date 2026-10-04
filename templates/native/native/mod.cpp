#include <cstdio>

using LogFn = void (*)(const char*);

static LogFn log_info;
static unsigned long long frames;

extern "C" __declspec(dllexport) void mod_init(LogFn log)
{
	log_info = log;
}

extern "C" __declspec(dllexport) void mod_update()
{
	frames++;
}

extern "C" __declspec(dllexport) void mod_shutdown()
{
	if (log_info)
	{
		char buffer[64];
		snprintf(buffer, sizeof buffer, "ModName native core ran for %llu frames", frames);
		log_info(buffer);
	}
}
